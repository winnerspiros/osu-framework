// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using AVFoundation;
using Foundation;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using osu.Framework.Logging;
using SDL;
using UIKit;
using static SDL.SDL3;

namespace osu.Framework.iOS
{
    /// <summary>
    /// Base <see cref="UIApplicationDelegate"/> implementation for osu!framework applications.
    /// </summary>
    public abstract class GameApplicationDelegate : UIResponder, IUIApplicationDelegate
    {
        internal event Action<string>? DragDrop;

        private const string output_volume = "outputVolume";

        private static readonly OutputVolumeObserver output_volume_observer = new OutputVolumeObserver();

        public IOSGameHost Host { get; private set; } = null!;

        public virtual bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
        {
            mapLibraryNames();

            SDL_SetMainReady();
            SDL_SetiOSEventPump(true);

            var audioSession = AVAudioSession.SharedInstance();

            // Use Playback category for rhythm-game audio: ignores the mute switch and avoids
            // audio interruptions. DuckOthers lowers other app audio rather than stopping it.
            audioSession.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.DuckOthers, out _);

            // Request low-latency I/O buffer duration (~5ms / 240 samples at 48kHz).
            // CoreAudio will honour the closest hardware-supported value.
            audioSession.SetPreferredIOBufferDuration(0.005, out _);

            // Request 48kHz sample rate for consistency with BASS engine defaults.
            audioSession.SetPreferredSampleRate(48000, out _);

            audioSession.SetActive(true, out _);

            // Observe volume changes to track user interaction.
            audioSession.AddObserver(output_volume_observer, output_volume, NSKeyValueObservingOptions.New, 0);

            // Register for thermal state notifications to allow the game to throttle.
            NSNotificationCenter.DefaultCenter.AddObserver(
                new NSString("NSProcessInfoThermalStateDidChangeNotification"),
                OnThermalStateChanged,
                NSProcessInfo.ProcessInfo);

            Host = new IOSGameHost();
            Host.Run(CreateGame());
            return true;
        }

        /// <summary>
        /// Called when the system thermal state changes. Games should reduce frame rate and
        /// GPU workload when thermal state is serious or critical to prevent forced throttling.
        /// </summary>
        protected virtual void OnThermalStateChanged(NSNotification notification)
        {
            var state = NSProcessInfo.ProcessInfo.ThermalState;

            switch (state)
            {
                case NSProcessInfoThermalState.Critical:
                    Logger.Log("iOS thermal state: CRITICAL — recommend reducing to 30 FPS.", LoggingTarget.Runtime, LogLevel.Important);
                    break;

                case NSProcessInfoThermalState.Serious:
                    Logger.Log("iOS thermal state: Serious — recommend reducing to 60 FPS.", LoggingTarget.Runtime, LogLevel.Important);
                    break;

                case NSProcessInfoThermalState.Fair:
                    Logger.Log("iOS thermal state: Fair — performance may be limited.", LoggingTarget.Runtime, LogLevel.Debug);
                    break;

                default:
                    Logger.Log("iOS thermal state: Nominal.", LoggingTarget.Runtime, LogLevel.Debug);
                    break;
            }
        }

        /// <summary>
        /// Called when the system is running low on memory. iOS has no swap, so this is the
        /// last chance to free resources before the app is killed by the OS.
        /// </summary>
        [Export("applicationDidReceiveMemoryWarning:")]
        public void DidReceiveMemoryWarning(UIApplication application)
        {
            Logger.Log("iOS memory warning received — forcing GC and requesting resource eviction.", LoggingTarget.Runtime, LogLevel.Important);

            // Force aggressive garbage collection
            GC.Collect(2, GCCollectionMode.Aggressive, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, true);
        }

        public virtual bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            // copied verbatim from SDL: https://github.com/libsdl-org/SDL/blob/d252a8fe126b998bd1b0f4e4cf52312cd11de378/src/video/uikit/SDL_uikitappdelegate.m#L508-L535
            // the hope is that the SDL app delegate class does not have such handling exist there, but Apple does not provide a corresponding notification to make that possible.
            NSUrl? fileUrl = url.FilePathUrl;
            DragDrop?.Invoke(fileUrl != null ? fileUrl.Path! : url.AbsoluteString!);
            return true;
        }

        public override void BuildMenu(IUIMenuBuilder builder)
        {
            base.BuildMenu(builder);

            // Remove useless menus on iPadOS. This makes it almost match macOS, displaying only "Window" and "Help".
            builder.RemoveMenu(UIMenuIdentifier.File.GetConstant()!);
            builder.RemoveMenu(UIMenuIdentifier.Edit.GetConstant()!);
            builder.RemoveMenu(UIMenuIdentifier.Format.GetConstant()!);
            builder.RemoveMenu(UIMenuIdentifier.View.GetConstant()!);
        }

        /// <summary>
        /// Creates the <see cref="Game"/> class to launch.
        /// </summary>
        protected abstract Game CreateGame();

        private static void mapLibraryNames()
        {
            NativeLibrary.SetDllImportResolver(typeof(Bass).Assembly, (_, assembly, path) => NativeLibrary.Load("@rpath/bass.framework/bass", assembly, path));
            NativeLibrary.SetDllImportResolver(typeof(BassFx).Assembly, (_, assembly, path) => NativeLibrary.Load("@rpath/bass_fx.framework/bass_fx", assembly, path));
            NativeLibrary.SetDllImportResolver(typeof(BassMix).Assembly, (_, assembly, path) => NativeLibrary.Load("@rpath/bassmix.framework/bassmix", assembly, path));
            NativeLibrary.SetDllImportResolver(typeof(SDL3).Assembly, (_, assembly, path) => NativeLibrary.Load("@rpath/SDL3.framework/SDL3", assembly, path));
        }

        private class OutputVolumeObserver : NSObject
        {
            public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, nint context)
            {
                switch (keyPath)
                {
                    case output_volume:
                        AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Playback);
                        break;
                }
            }
        }
    }
}
