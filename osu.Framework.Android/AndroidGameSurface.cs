// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Org.Libsdl.App;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Bindables;
using Android.Views;
using AndroidX.Core.View;
using AndroidX.Window.Layout;

namespace osu.Framework.Android
{
    internal class AndroidGameSurface : SDLSurface
    {
        private AndroidGameActivity activity { get; } = null!;

        public BindableSafeArea SafeAreaPadding { get; } = new BindableSafeArea();

        public AndroidGameSurface(AndroidGameActivity activity, Context? context)
            : base(context)
        {
            init();
            this.activity = activity;
        }

        protected AndroidGameSurface(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
            init();
        }

        private void init()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                // disable ugly green border when view is focused via hardware keyboard/mouse.
                DefaultFocusHighlightEnabled = false;
            }
        }

        // Surface lifecycle gating.
        //
        // The Veldrid Vulkan backend cannot tolerate drawing into an Android ANativeWindow that
        // has not yet been sized, or one that has been destroyed underneath it. Some OEMs (notably
        // Adreno-based devices) also report a 0×0 surface during the first surfaceCreated and
        // only deliver real dimensions in the subsequent surfaceChanged. Driving a swapchain off
        // the surfaceCreated edge alone produces a black / 90°-rotated frame on those devices.
        //
        // We therefore AND-gate IsSurfaceReady on:
        //   1. surfaceCreated having fired (raw native window exists), and
        //   2. surfaceChanged having reported a non-zero size, and
        //   3. the SDL app lifecycle being resumed (HandleResume).
        //
        // On surfaceDestroyed we reset all three and block the JNI thread for a bounded time
        // until the Draw thread acknowledges that it has stopped using the surface — this gives
        // any in-flight DrawFrame a chance to finish before Android tears down the underlying
        // ANativeWindow, eliminating the surface-lost recovery loop on the rotate / lock paths.
        private volatile bool surfaceCreated;
        private volatile bool surfaceHasSize;
        private volatile bool isResumed = true;
        private volatile int surfaceWidth;
        private volatile int surfaceHeight;

        public bool IsSurfaceReady => surfaceCreated && surfaceHasSize && isResumed;

        public System.Drawing.Size SurfaceSize => new System.Drawing.Size(surfaceWidth, surfaceHeight);

        /// <summary>
        /// Signalled by the Draw thread once it has observed <see cref="IsSurfaceReady"/> as
        /// false and skipped a frame. Allows the JNI thread blocked in
        /// <see cref="SurfaceDestroyed(ISurfaceHolder)"/> to safely return to Android.
        /// </summary>
        private readonly ManualResetEventSlim drawThreadAcknowledgedTeardown = new ManualResetEventSlim(true);

        /// <summary>
        /// Bound on how long the JNI thread is allowed to wait for the Draw thread to drain.
        /// Kept well below the 5s ANR threshold so a stuck Draw thread can never wedge Android.
        /// </summary>
        private const int teardown_wait_timeout_ms = 250;

        /// <summary>
        /// Called from the Draw thread (via <see cref="AndroidGameHost.DrawFrame"/>) every time
        /// it sees <see cref="IsSurfaceReady"/> as false. Releases anyone blocked in
        /// <see cref="SurfaceDestroyed"/>.
        /// </summary>
        public void NotifyDrawThreadIdle() => drawThreadAcknowledgedTeardown.Set();

        protected override void HandlePause()
        {
            base.HandlePause();
            isResumed = false;
        }

        protected override void HandleResume()
        {
            base.HandleResume();
            isResumed = true;
        }

        public override void SurfaceCreated(ISurfaceHolder? holder)
        {
            base.SurfaceCreated(holder);
            surfaceCreated = true;
            // Don't flip surfaceHasSize here — wait for surfaceChanged so we use real dimensions.
        }

        public override void SurfaceChanged(ISurfaceHolder? holder, [GeneratedEnum] Format format, int width, int height)
        {
            base.SurfaceChanged(holder, format, width, height);
            surfaceWidth = width;
            surfaceHeight = height;
            surfaceHasSize = width > 0 && height > 0;
        }

        public override void SurfaceDestroyed(ISurfaceHolder? holder)
        {
            // Mark the surface as gone first so the Draw thread short-circuits its next frame.
            surfaceCreated = false;
            surfaceHasSize = false;
            surfaceWidth = 0;
            surfaceHeight = 0;

            // Ask the Draw thread to acknowledge that it has observed the teardown before we
            // let Android free the underlying ANativeWindow. The Draw thread sets the event in
            // AndroidGameHost.DrawFrame when it sees IsSurfaceReady == false.
            drawThreadAcknowledgedTeardown.Reset();

            try
            {
                if (!drawThreadAcknowledgedTeardown.Wait(teardown_wait_timeout_ms))
                    Logger.Log($"Draw thread did not acknowledge surface teardown within {teardown_wait_timeout_ms}ms; proceeding anyway to avoid ANR.", level: LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to wait for Draw thread surface teardown: {ex.Message}", level: LogLevel.Important);
            }

            base.SurfaceDestroyed(holder);
        }

        public override WindowInsets? OnApplyWindowInsets(View? view, WindowInsets? insets)
        {
            updateSafeArea(insets);
            return base.OnApplyWindowInsets(view, insets);
        }

        /// <summary>
        /// Updates the <see cref="IWindow.SafeAreaPadding"/>, taking into account screen insets that may be obstructing this <see cref="AndroidGameSurface"/>.
        /// </summary>
        private void updateSafeArea(WindowInsets? windowInsets)
        {
            if (activity == null) return;
            var metrics = WindowMetricsCalculator.Companion.OrCreate.ComputeCurrentWindowMetrics(activity);
            var windowArea = metrics.Bounds.ToRectangleI();
            var usableWindowArea = windowArea;

            if (OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                var cutout = windowInsets?.DisplayCutout;

                if (cutout != null)
                    usableWindowArea = usableWindowArea.Shrink(cutout.SafeInsetLeft, cutout.SafeInsetRight, cutout.SafeInsetTop, cutout.SafeInsetBottom);
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(31) && windowInsets != null)
            {
                var topLeftCorner = windowInsets.GetRoundedCorner((int)RoundedCornerPosition.TopLeft);
                var topRightCorner = windowInsets.GetRoundedCorner((int)RoundedCornerPosition.TopRight);
                var bottomLeftCorner = windowInsets.GetRoundedCorner((int)RoundedCornerPosition.BottomLeft);
                var bottomRightCorner = windowInsets.GetRoundedCorner((int)RoundedCornerPosition.BottomRight);

                int cornerInsetLeft = Math.Max(topLeftCorner?.Radius ?? 0, bottomLeftCorner?.Radius ?? 0);
                int cornerInsetRight = Math.Max(topRightCorner?.Radius ?? 0, bottomRightCorner?.Radius ?? 0);
                int cornerInsetTop = Math.Max(topLeftCorner?.Radius ?? 0, topRightCorner?.Radius ?? 0);
                int cornerInsetBottom = Math.Max(bottomLeftCorner?.Radius ?? 0, bottomRightCorner?.Radius ?? 0);

                var radiusInsetArea = windowArea.Width >= windowArea.Height
                    ? windowArea.Shrink(cornerInsetLeft, cornerInsetRight, 0, 0)
                    : windowArea.Shrink(0, 0, cornerInsetTop, cornerInsetBottom);

                usableWindowArea = usableWindowArea.Intersect(radiusInsetArea);
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(24) && activity.IsInMultiWindowMode && windowInsets != null)
            {
                // if we are in multi-window mode, the status bar is always visible (even if we request to hide it) and could be obstructing our view.
                // if multi-window mode is not active, we can assume the status bar is hidden so we shouldn't consider it for safe area calculations.
                var insetsCompat = WindowInsetsCompat.ToWindowInsetsCompat(windowInsets, this);
                int statusBarHeight = insetsCompat?.GetInsets(WindowInsetsCompat.Type.StatusBars())?.Top ?? 0;
                usableWindowArea = usableWindowArea.Intersect(windowArea.Shrink(0, 0, statusBarHeight, 0));
            }

            SafeAreaPadding.Value = new MarginPadding
            {
                Left = usableWindowArea.Left - windowArea.Left,
                Top = usableWindowArea.Top - windowArea.Top,
                Right = windowArea.Right - usableWindowArea.Right,
                Bottom = windowArea.Bottom - usableWindowArea.Bottom,
            };
        }
    }
}
