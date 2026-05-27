// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Android.App;
using Android.Content;
using AndroidX.Core.Content;
using osu.Framework.Android.Graphics.Textures;
using osu.Framework.Android.Graphics.Video;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Stream = System.IO.Stream;
using Uri = Android.Net.Uri;

namespace osu.Framework.Android
{
    public class AndroidGameHost : SDLGameHost
    {
        private readonly AndroidGameActivity activity;

        public AndroidGameHost(AndroidGameActivity activity)
            : base(string.Empty)
        {
            this.activity = activity;
        }

        protected override void SetupForRun()
        {
            base.SetupForRun();

            // Set the main thread to THREAD_PRIORITY_DISPLAY (-4) for scheduler prioritisation.
            // The main thread drives input/UI work and (in SingleThread mode) all game threads
            // including rendering. Boosting it gives that work higher scheduler priority.
            try
            {
                global::Android.OS.Process.SetThreadPriority(global::Android.OS.ThreadPriority.Display);
                Logger.Log("Android thread priority set to THREAD_PRIORITY_DISPLAY.", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to set Android thread priority: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            // Use SustainedLowLatency GC mode to prevent Gen2 collections during gameplay.
            // This reduces frame hitches caused by stop-the-world pauses on mobile where
            // frame budgets are tight (8.3ms at 120Hz).
            try
            {
                System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
                Logger.Log("GC latency mode set to SustainedLowLatency.", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to set GC latency mode: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            // Register thermal status listener on API 29+ (Android 10+) to detect thermal throttling.
            registerThermalListener();
        }

        protected override IWindow CreateWindow(GraphicsSurfaceType preferredSurface) => new AndroidGameWindow(preferredSurface, Options.FriendlyGameName);

        private bool drawThreadPrioritySet;

        // PerformanceHintSession state — only used on API 31+ (Android 12+).
        // Tells the Android scheduler the target and actual frame durations so it can
        // choose appropriate CPU clock frequencies without relying solely on thread priority.
        private global::Android.OS.PerformanceHintManager.Session? hintSession;
        private bool hintSessionInitialised;
        private double lastSessionTargetHz;

        protected override void DrawFrame()
        {
            // Boost the draw thread to THREAD_PRIORITY_DISPLAY on the first call so it
            // matches the input thread priority set in SetupForRun. The draw thread is
            // created after SetupForRun, so we can't set it there.
            if (!drawThreadPrioritySet)
            {
                drawThreadPrioritySet = true;

                try
                {
                    global::Android.OS.Process.SetThreadPriority(global::Android.OS.ThreadPriority.Display);
                    Logger.Log("Android draw thread priority set to THREAD_PRIORITY_DISPLAY.", LoggingTarget.Runtime, LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to set Android draw thread priority: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }

            // Create or update the PerformanceHintSession once we're on the draw thread.
            if (!hintSessionInitialised)
            {
                hintSessionInitialised = true;
                tryInitPerformanceHintSession();
            }
            else if (hintSession != null)
            {
                updateHintSessionTargetIfNeeded();
            }

            var surface = AndroidGameActivity.Surface;

            // Capture the active session and timestamp together so that the
            // Stopwatch.GetTimestamp() syscall is skipped on frames where no hint session
            // is active, and so the compiler can verify the null-safety of the report call.
            var activeSession = hintSession;
            long workStart = activeSession != null && surface.IsSurfaceReady ? Stopwatch.GetTimestamp() : 0;

            if (surface.IsSurfaceReady)
            {
                base.DrawFrame();
            }
            else
            {
                // Surface is not in a drawable state (just created and not yet sized, paused,
                // or being torn down). Release any JNI thread blocked in
                // AndroidGameSurface.SurfaceDestroyed waiting for us to drain — we have skipped
                // a frame, so no GPU work is in flight against the doomed surface.
                surface.NotifyDrawThreadIdle();
            }

            // Report actual frame work duration to the hint session so the Android scheduler
            // can tune CPU clock frequency to match the target. This is called after DrawFrame
            // so the measurement includes GPU submission but excludes VSync idle wait.
            if (workStart != 0)
            {
                long workEnd = Stopwatch.GetTimestamp();
                long actualDurationNs = (long)((workEnd - workStart) * (1_000_000_000.0 / Stopwatch.Frequency));

                try
                {
                    activeSession!.ReportActualWorkDuration(actualDurationNs);
                }
                catch (Exception ex)
                {
                    Logger.Log($"PerformanceHintSession.ReportActualWorkDuration failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                    hintSession = null;
                }
            }
        }

        /// <summary>
        /// Attempts to create an Android <c>PerformanceHintSession</c> (API 31+) for the current
        /// draw thread. The session tells the OS scheduler the expected frame work duration so it
        /// can keep CPU clocks high enough without busy-spinning. Silently does nothing on older
        /// API levels or if the system service is unavailable.
        /// </summary>
        [global::System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
        private void tryInitPerformanceHintSession()
        {
            if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.S)
                return;

            try
            {
                var manager = activity.ApplicationContext?.GetSystemService("performance_hint")
                    as global::Android.OS.PerformanceHintManager;

                if (manager == null)
                {
                    Logger.Log("PerformanceHintManager not available on this device.", LoggingTarget.Runtime, LogLevel.Debug);
                    return;
                }

                long targetDurationNs = computeTargetDurationNs();
                lastSessionTargetHz = effectiveTargetHz();

                hintSession = manager.CreateHintSession(new[] { global::Android.OS.Process.MyTid() }, targetDurationNs);

                Logger.Log($"Android PerformanceHintSession created (target: {targetDurationNs / 1_000_000.0:F2} ms).",
                    LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create Android PerformanceHintSession: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        /// <summary>
        /// Updates the session's target work duration when <see cref="GameHost.MaximumDrawHz"/> has
        /// changed since the session was last configured.
        /// </summary>
        [global::System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
        private void updateHintSessionTargetIfNeeded()
        {
            double currentHz = effectiveTargetHz();

            if (Math.Abs(currentHz - lastSessionTargetHz) < 0.5)
                return;

            lastSessionTargetHz = currentHz;

            try
            {
                // Use the already-computed hz to avoid a second effectiveTargetHz() call
                // (which may invoke a JNI RefreshRate query when the draw rate is uncapped).
                hintSession!.UpdateTargetWorkDuration((long)(1_000_000_000.0 / currentHz));
            }
            catch (Exception ex)
            {
                Logger.Log($"PerformanceHintSession.UpdateTargetWorkDuration failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                hintSession = null;
            }
        }

        /// <summary>
        /// Returns the draw Hz that should be used as the hint-session target.
        /// Falls back to the display's natural refresh rate when <see cref="GameHost.MaximumDrawHz"/>
        /// is uncapped (> 360) or zero.
        /// </summary>
        private double effectiveTargetHz()
        {
            double drawHz = MaximumDrawHz;

            if (drawHz > 0 && drawHz <= 360)
                return drawHz;

            // Uncapped or unreasonably high — use the display's own refresh rate.
            float displayHz = activity.WindowManager?.DefaultDisplay?.RefreshRate ?? 0f;
            return displayHz > 0 ? displayHz : 120.0;
        }

        private long computeTargetDurationNs()
        {
            double hz = effectiveTargetHz();
            return (long)(1_000_000_000.0 / hz);
        }

        public override bool CanExit => false;

        public override bool CanSuspendToBackground => true;

        public override bool OnScreenKeyboardOverlapsGameWindow => true;

        public override Storage GetStorage(string path) => new AndroidStorage(path, this);

        public override IEnumerable<string> UserStoragePaths
            // not null as internal "external storage" is always available.
            => Application.Context.GetExternalFilesDir(string.Empty).AsNonNull().ToString().Yield();

        public override ISystemFileSelector CreateSystemFileSelector(string[] allowedExtensions)
            => new AndroidFileSelector(activity, allowedExtensions);

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// On Android, this method and <see cref="PresentFileExternally"/> have the same behaviour.
        /// </para>
        /// <para>
        /// Because of Android's stringent restrictions on accessing files on the device,
        /// this method will pretty much only work on files that the game directly controls or creates in its dedicated storages,
        /// and even then, only if they are explicitly allowlisted as accessible via a <c>FileProvider</c>.
        /// See provided example below for how to set up sharing.
        /// </para>
        /// <para>
        /// If this method is prompted to open a file that is not in this game's storages, or the file path is not whitelisted, this method will return <see langword="false"/>
        /// and log the appropriate error.
        /// </para>
        /// </remarks>
        /// <example>
        /// <para>
        /// In <c>AndroidManifest.xml</c>:
        /// <code>
        /// &lt;manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.MyGame" android:installLocation="auto"&gt;
        ///     &lt;application android:label="osu!framework test"&gt;
        ///          &lt;provider android:name="androidx.core.content.FileProvider"
        ///                    android:authorities="com.example.MyGame.fileprovider"
        ///                    android:grantUriPermissions="true"
        ///                    android:exported="false"&gt;
        ///               &lt;meta-data android:name="android.support.FILE_PROVIDER_PATHS"
        ///                          android:resource="@xml/filepaths" /&gt;
        ///          &lt;/provider&gt;
        ///     &lt;/application&gt;
        /// &lt;/manifest&gt;
        /// </code>
        /// Note that the authority of the file provider MUST be the package name suffixed with <c>.fileprovider</c>.
        /// </para>
        /// <para>
        /// In <c>Resources/xml/filepaths.xml</c>:
        /// <code>
        /// &lt;?xml version="1.0" encoding="utf-8"?&gt;
        /// &lt;paths&gt;
        ///      &lt;external-files-path path="logs" name="logs" /&gt;
        /// &lt;/paths&gt;
        /// </code>
        /// Paths in <c>&lt;external-files-path&gt;</c> tags are relative to the only path in <see cref="UserStoragePaths"/>.
        /// </para>
        /// </example>
        public override bool OpenFileExternally(string filename)
        {
            var context = activity.ApplicationContext!;
            Java.IO.File file = new Java.IO.File(filename);
            Uri? contentUri;

            try
            {
                contentUri = FileProvider.GetUriForFile(context, $"{context.PackageName}.fileprovider", file);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create content URI for file: {filename}.\nError: {ex}");
                return false;
            }

            if (contentUri == null)
                return false;

            // https://developer.android.com/training/sharing/send#send-binary-content
            // https://developer.android.com/reference/android/content/Intent#ACTION_SEND
            var shareIntent = new Intent(Intent.ActionSend);
            shareIntent.PutExtra(Intent.ExtraStream, contentUri);
            shareIntent.SetType(activity.ContentResolver?.GetType(contentUri));
            activity.StartActivity(Intent.CreateChooser(shareIntent, "Share"));
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// On Android, this method and <see cref="OpenFileExternally"/> have the same behaviour.
        /// See remarks of that method for instructions how to set up file sharing on Android.
        /// </remarks>
        public override bool PresentFileExternally(string filename) => OpenFileExternally(filename);

        public override void OpenUrlExternally(string url)
        {
            if (!url.CheckIsValidUrl())
                throw new ArgumentException("The provided URL must be one of either http://, https:// or mailto: protocols.", nameof(url));

            try
            {
                using (var intent = new Intent(Intent.ActionView, Uri.Parse(url)))
                {
                    // Recommended way to open URLs on Android 11+
                    // https://developer.android.com/training/package-visibility/use-cases#open-urls-browser-or-other-app
                    activity.StartActivity(intent);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unable to open external link.");
            }
        }

        public override IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
            => new AndroidTextureLoaderStore(underlyingStore);

        public override VideoDecoder CreateVideoDecoder(Stream stream)
            => new AndroidVideoDecoder(Renderer, stream);

        public override bool SuspendToBackground()
        {
            return activity.MoveTaskToBack(true);
        }

        #region Thermal monitoring

        /// <summary>
        /// Registers a thermal status listener on API 29+ that logs thermal state changes.
        /// Games can override <see cref="OnThermalStatusChanged"/> to reduce workload.
        /// </summary>
        private void registerThermalListener()
        {
            if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.Q)
                return;

            try
            {
                var powerManager = activity.GetSystemService(Context.PowerService) as global::Android.OS.PowerManager;

                if (powerManager == null)
                {
                    Logger.Log("PowerManager not available for thermal monitoring.", LoggingTarget.Runtime, LogLevel.Debug);
                    return;
                }

                powerManager.AddThermalStatusListener(activity.MainExecutor!, new ThermalStatusListener(this));
                Logger.Log("Android thermal status listener registered.", LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to register thermal listener: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        /// <summary>
        /// Called when the device thermal status changes. Override in derived hosts to
        /// reduce frame rate or GPU workload under thermal pressure.
        /// </summary>
        /// <param name="status">The new thermal status (0=None, 1=Light, 2=Moderate, 3=Severe, 4=Critical, 5=Emergency, 6=Shutdown).</param>
        protected virtual void OnThermalStatusChanged(int status)
        {
            string label = status switch
            {
                0 => "None",
                1 => "Light",
                2 => "Moderate",
                3 => "Severe",
                4 => "Critical",
                5 => "Emergency",
                6 => "Shutdown",
                _ => $"Unknown({status})"
            };

            var level = status >= 3 ? LogLevel.Important : LogLevel.Debug;
            Logger.Log($"Android thermal status changed: {label} (level={status})", LoggingTarget.Runtime, level);
        }

        private class ThermalStatusListener : Java.Lang.Object, global::Android.OS.PowerManager.IOnThermalStatusChangedListener
        {
            private readonly AndroidGameHost host;

            public ThermalStatusListener(AndroidGameHost host)
            {
                this.host = host;
            }

            public void OnThermalStatusChanged(int status)
            {
                host.OnThermalStatusChanged(status);
            }
        }

        #endregion
    }
}
