// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using osu.Framework.Graphics.Rendering.Deferred.Allocation;
using osu.Framework.Graphics.Rendering.Deferred.Events;
using osu.Framework.Graphics.Veldrid.Buffers;
using osu.Framework.Graphics.Veldrid.Pipelines;
using osu.Framework.Graphics.Veldrid.Textures;
using osu.Framework.Statistics;

namespace osu.Framework.Graphics.Rendering.Deferred
{
    /// <summary>
    /// Processes the render events for a single frame.
    /// </summary>
    internal readonly ref struct EventProcessor
    {
        private static readonly GlobalStatistic<int> stat_dead_state_events_skipped =
            GlobalStatistics.Get<int>(nameof(EventProcessor), "Dead state events skipped");

        private readonly DeferredContext context;
        private readonly GraphicsPipeline graphics;

        public EventProcessor(DeferredContext context)
        {
            this.context = context;
            graphics = context.Renderer.Graphics;
        }

        public void ProcessEvents()
        {
            printEventsForDebug();
            processUploads();
            processEvents();
        }

        private void printEventsForDebug()
        {
            if (string.IsNullOrEmpty(FrameworkEnvironment.DeferredRendererEventsOutputPath))
                return;

            StringBuilder builder = new StringBuilder();
            int indent = 0;

            foreach (var renderEvent in context.RenderEvents)
            {
                string info;
                int indentChange = 0;

                switch (renderEvent.Type)
                {
                    case RenderEventType.DrawNodeAction:
                    {
                        DrawNodeActionEvent e = (DrawNodeActionEvent)renderEvent;

                        info = $"DrawNode.{e.Action} ({context.Dereference<DrawNode>(e.DrawNode)})";

                        switch (e.Action)
                        {
                            case DrawNodeActionType.Enter:
                                indentChange += 2;
                                break;

                            case DrawNodeActionType.Exit:
                                indentChange -= 2;
                                break;
                        }

                        break;
                    }

                    default:
                    {
                        info = $"{renderEvent.Type.ToString()}";
                        break;
                    }
                }

                indent += Math.Min(0, indentChange);
                builder.AppendLine($"{new string(' ', indent)}{info}");
                indent += Math.Max(0, indentChange);
            }

            File.WriteAllText(FrameworkEnvironment.DeferredRendererEventsOutputPath, builder.ToString());
        }

        private void processUploads()
        {
            for (int i = 0; i < context.RenderEvents.Count; i++)
            {
                var renderEvent = context.RenderEvents[i];

                switch (renderEvent.Type)
                {
                    case RenderEventType.AddPrimitiveToBatch:
                    {
                        AddPrimitiveToBatchEvent e = (AddPrimitiveToBatchEvent)renderEvent;
                        IDeferredVertexBatch batch = context.Dereference<IDeferredVertexBatch>(e.VertexBatch);
                        batch.Write(e.Memory);
                        break;
                    }

                    case RenderEventType.SetUniformBufferData:
                    {
                        SetUniformBufferDataEvent e = (SetUniformBufferDataEvent)renderEvent;
                        IDeferredUniformBuffer buffer = context.Dereference<IDeferredUniformBuffer>(e.Buffer);
                        UniformBufferReference range = buffer.Write(e.Data);
                        context.RenderEvents[i] = RenderEvent.Create(new SetUniformBufferDataRangeEvent(e.Buffer, range));
                        break;
                    }

                    case RenderEventType.SetShaderStorageBufferObjectData:
                    {
                        SetShaderStorageBufferObjectDataEvent e = (SetShaderStorageBufferObjectDataEvent)renderEvent;
                        IDeferredShaderStorageBufferObject buffer = context.Dereference<IDeferredShaderStorageBufferObject>(e.Buffer);
                        buffer.Write(e.Index, e.Memory);
                        break;
                    }
                }
            }

            context.VertexManager.Commit();
            context.UniformBufferManager.Commit();
        }

        private void processEvents()
        {
            int count = context.RenderEvents.Count;

            if (count == 0)
                return;

            // Pre-pass: identify single-slot pipeline-state events that are overridden before the
            // next Flush or Clear without any intervening draw. These "dead" events produce no
            // visible effect and can be skipped, reducing GPU command-buffer overhead.
            byte[] skipFlags = ArrayPool<byte>.Shared.Rent(count);
            Array.Clear(skipFlags, 0, count);
            stat_dead_state_events_skipped.Value = eliminateDeadStateEvents(skipFlags, count);

            for (int i = 0; i < count; i++)
            {
                if (skipFlags[i] != 0)
                    continue;

                var renderEvent = context.RenderEvents[i];

                switch (renderEvent.Type)
                {
                    case RenderEventType.SetFrameBuffer:
                    {
                        processEvent((SetFrameBufferEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.ResizeFrameBuffer:
                    {
                        processEvent((ResizeFrameBufferEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetShader:
                    {
                        processEvent((SetShaderEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetTexture:
                    {
                        processEvent((SetTextureEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetUniformBuffer:
                    {
                        processEvent((SetUniformBufferEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.Clear:
                    {
                        processEvent((ClearEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetDepthInfo:
                    {
                        processEvent((SetDepthInfoEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetScissor:
                    {
                        processEvent((SetScissorEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetScissorState:
                    {
                        processEvent((SetScissorStateEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetStencilInfo:
                    {
                        processEvent((SetStencilInfoEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetViewport:
                    {
                        processEvent((SetViewportEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetBlend:
                    {
                        processEvent((SetBlendEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetBlendMask:
                    {
                        processEvent((SetBlendMaskEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.Flush:
                    {
                        processEvent((FlushEvent)renderEvent);
                        break;
                    }

                    case RenderEventType.SetUniformBufferData:
                    {
                        Debug.Fail("Uniform buffers should be uploaded during the pre-draw upload process.");
                        break;
                    }

                    case RenderEventType.SetUniformBufferDataRange:
                    {
                        processEvent((SetUniformBufferDataRangeEvent)renderEvent);
                        break;
                    }
                }
            }

            ArrayPool<byte>.Shared.Return(skipFlags);
        }

        /// <summary>
        /// Forward-scans the event list and marks single-slot pipeline state events as dead
        /// (skipFlags[i] = 1) when they are overridden by a subsequent event of the same type
        /// before the next <see cref="RenderEventType.Flush"/> or <see cref="RenderEventType.Clear"/>.
        /// This reduces the number of GPU state-change commands issued per frame.
        /// </summary>
        /// <returns>The number of events marked as dead.</returns>
        private int eliminateDeadStateEvents(byte[] skipFlags, int count)
        {
            int deadCount = 0;

            // For each single-slot state type: index of the most recent pending event
            // (-1 = no pending event of this type). "Pending" means not yet consumed by
            // a Flush or Clear.
            int lastScissor = -1;
            int lastScissorState = -1;
            int lastDepth = -1;
            int lastStencil = -1;
            int lastBlend = -1;
            int lastBlendMask = -1;
            int lastViewport = -1;
            int lastShader = -1;
            int lastFrameBuffer = -1;

            for (int i = 0; i < count; i++)
            {
                switch (context.RenderEvents[i].Type)
                {
                    case RenderEventType.SetScissor:
                        if (lastScissor >= 0) { skipFlags[lastScissor] = 1; deadCount++; }

                        lastScissor = i;
                        break;

                    case RenderEventType.SetScissorState:
                        if (lastScissorState >= 0) { skipFlags[lastScissorState] = 1; deadCount++; }

                        lastScissorState = i;
                        break;

                    case RenderEventType.SetDepthInfo:
                        if (lastDepth >= 0) { skipFlags[lastDepth] = 1; deadCount++; }

                        lastDepth = i;
                        break;

                    case RenderEventType.SetStencilInfo:
                        if (lastStencil >= 0) { skipFlags[lastStencil] = 1; deadCount++; }

                        lastStencil = i;
                        break;

                    case RenderEventType.SetBlend:
                        if (lastBlend >= 0) { skipFlags[lastBlend] = 1; deadCount++; }

                        lastBlend = i;
                        break;

                    case RenderEventType.SetBlendMask:
                        if (lastBlendMask >= 0) { skipFlags[lastBlendMask] = 1; deadCount++; }

                        lastBlendMask = i;
                        break;

                    case RenderEventType.SetViewport:
                        if (lastViewport >= 0) { skipFlags[lastViewport] = 1; deadCount++; }

                        lastViewport = i;
                        break;

                    case RenderEventType.SetShader:
                        if (lastShader >= 0) { skipFlags[lastShader] = 1; deadCount++; }

                        lastShader = i;
                        break;

                    case RenderEventType.SetFrameBuffer:
                        if (lastFrameBuffer >= 0) { skipFlags[lastFrameBuffer] = 1; deadCount++; }

                        lastFrameBuffer = i;
                        break;

                    case RenderEventType.Flush:
                    case RenderEventType.Clear:
                        // All pending state events have been consumed — they're no longer dead.
                        lastScissor = lastScissorState = lastDepth = lastStencil = -1;
                        lastBlend = lastBlendMask = lastViewport = lastShader = lastFrameBuffer = -1;
                        break;
                }
            }

            return deadCount;
        }

        private void processEvent(in SetFrameBufferEvent e)
            => graphics.SetFrameBuffer(context.Dereference<DeferredFrameBuffer?>(e.FrameBuffer));

        private void processEvent(in ResizeFrameBufferEvent e)
            => context.Dereference<DeferredFrameBuffer>(e.FrameBuffer).Resize(e.Size);

        private void processEvent(in SetShaderEvent e)
            => graphics.SetShader(context.Dereference<DeferredShader>(e.Shader).Resource);

        private void processEvent(in SetTextureEvent e)
            => graphics.AttachTexture(e.Unit, context.Dereference<IVeldridTexture>(e.Texture));

        private void processEvent(in SetUniformBufferEvent e)
            => graphics.AttachUniformBuffer(context.Dereference<string>(e.Name), context.Dereference<IVeldridUniformBuffer>(e.Buffer));

        private void processEvent(in ClearEvent e)
            => graphics.Clear(e.Info);

        private void processEvent(in SetDepthInfoEvent e)
            => graphics.SetDepthInfo(e.Info);

        private void processEvent(in SetScissorEvent e)
            => graphics.SetScissor(e.Scissor);

        private void processEvent(in SetScissorStateEvent e)
            => graphics.SetScissorState(e.Enabled);

        private void processEvent(in SetStencilInfoEvent e)
            => graphics.SetStencilInfo(e.Info);

        private void processEvent(in SetViewportEvent e)
            => graphics.SetViewport(e.Viewport);

        private void processEvent(in SetBlendEvent e)
            => graphics.SetBlend(e.Parameters);

        private void processEvent(in SetBlendMaskEvent e)
            => graphics.SetBlendMask(e.Mask);

        private void processEvent(in FlushEvent e)
            => context.Dereference<IDeferredVertexBatch>(e.VertexBatch).Draw(e.VertexCount);

        private void processEvent(in SetUniformBufferDataRangeEvent e)
        {
            IDeferredUniformBuffer buffer = context.Dereference<IDeferredUniformBuffer>(e.Buffer);

            buffer.Activate(e.Range.Chunk);
            graphics.SetUniformBufferOffset(buffer, (uint)e.Range.OffsetInChunk);
        }
    }
}
