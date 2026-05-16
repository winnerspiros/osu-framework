// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace osu.Framework.Platform
{
    /// <summary>
    /// An inter-process communication provider that runs over a specified TCP port, binding to the loopback address.
    /// This single class handles both binding as a server, or messaging another bound instance that is acting as a server.
    /// </summary>
    public class TcpIpcProvider : IDisposable
    {
        /// <summary>
        /// Invoked when a message is received when running as a server.
        /// Returns either a response in the form of an <see cref="IpcMessage"/>, or <c>null</c> for no response.
        /// </summary>
        public event Func<IpcMessage, IpcMessage?>? MessageReceived;

        private Task? listenTask;

        private readonly CancellationTokenSource cancellationSource = new CancellationTokenSource();

        private readonly int port;

        /// <summary>
        /// Create a new provider.
        /// </summary>
        /// <param name="port">The port to operate on.</param>
        public TcpIpcProvider(int port)
        {
            this.port = port;
        }

        /// <summary>
        /// Attempt to bind to the TCP port as a server, and start listening for incoming connections if successful.
        /// </summary>
        /// <returns>
        /// Whether the bind was successful.
        /// If <c>false</c>, another instance is likely already running (and can be messaged using <see cref="SendMessageAsync"/> or <see cref="SendMessageWithResponseAsync"/>).
        /// </returns>
        public bool Bind()
        {
            if (listenTask != null)
                throw new InvalidOperationException($"Can't {nameof(Bind)} more than once.");

            var listener = new TcpListener(IPAddress.Loopback, port);

            try
            {
                listener.Start();

                listenTask = listenAsync(listener);

                return true;
            }
            catch (SocketException ex)
            {
                // In the common case that another instance is bound the the port, we don't need to log anything.
                if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    return false;

                Logger.Error(ex, "Unable to bind IPC server");
                return false;
            }
        }

        private async Task listenAsync(TcpListener listener)
        {
            var token = cancellationSource.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    using (client)
                    {
                        // Disable Nagle's algorithm for minimal loopback IPC latency.
                        client.NoDelay = true;

                        using (var stream = client.GetStream())
                        {
                            try
                            {
                                var message = await receive(stream, token).ConfigureAwait(false);

                                if (message == null)
                                    continue;

                                var response = MessageReceived?.Invoke(message);

                                if (response != null)
                                    await send(stream, response).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, "Error handling incoming IPC request.");
                            }
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Send a message to the IPC server.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public async Task SendMessageAsync(IpcMessage message)
        {
            using (var client = new TcpClient())
            {
                client.NoDelay = true;
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                using (var stream = client.GetStream())
                    await send(stream, message).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Send a message to the IPC server.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The response from the server.</returns>
        public async Task<IpcMessage?> SendMessageWithResponseAsync(IpcMessage message)
        {
            using (var client = new TcpClient())
            {
                client.NoDelay = true;
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                using (var stream = client.GetStream())
                {
                    await send(stream, message).ConfigureAwait(false);
                    return await receive(stream).ConfigureAwait(false);
                }
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "IPC serialization uses known types at runtime.")]
        private async Task send(Stream stream, IpcMessage message)
        {
            string str = JsonConvert.SerializeObject(message, Formatting.None);
            byte[] data = Encoding.UTF8.GetBytes(str);

            // Write header + payload as one buffer to avoid TCP fragmentation from two small writes.
            byte[] packet = new byte[sizeof(int) + data.Length];
            BitConverter.TryWriteBytes(packet.AsSpan(0, sizeof(int)), data.Length);
            data.CopyTo(packet.AsSpan(sizeof(int)));

            await stream.WriteAsync(packet.AsMemory()).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "IPC serialization uses known types at runtime.")]
        [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "IPC serialization uses known types at runtime.")]
        private async Task<IpcMessage?> receive(Stream stream, CancellationToken cancellationToken = default)
        {
            const int header_length = sizeof(int);

            byte[] header = new byte[header_length];

            int read = await stream.ReadAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);

            if (read < header_length)
                return null;

            int contentLength = BitConverter.ToInt32(header, 0);

            if (contentLength == 0)
                return null;

            byte[] data = await stream.ReadBytesToArrayAsync(contentLength, cancellationToken).ConfigureAwait(false);

            string str = Encoding.UTF8.GetString(data);

            var json = JToken.Parse(str);

            string typeName = json["Type"]?.Value<string>() ?? throw new InvalidOperationException("Response JSON has missing Type field.");

            var type = Type.GetType(typeName);
            var value = json["Value"];

            if (type == null) throw new InvalidOperationException($"Response type could not be mapped ({typeName}).");
            if (value == null) throw new InvalidOperationException("Response JSON has missing Value field.");

            return new IpcMessage
            {
                Type = type.AssemblyQualifiedName,
                Value = JsonConvert.DeserializeObject(value.ToString(), type),
            };
        }

        public void Dispose()
        {
            const int thread_join_timeout = 2000;

            if (listenTask != null)
            {
                cancellationSource.Cancel();
                if (!listenTask.Wait(thread_join_timeout))
                    Logger.Log($"IPC thread failed to exit in allocated time ({thread_join_timeout}ms).", LoggingTarget.Runtime, LogLevel.Important);
            }
        }
    }
}
