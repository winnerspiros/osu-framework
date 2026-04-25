// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Input;
using osu.Framework.Input.Handlers;
using osu.Framework.Input.Handlers.Joystick;
using osu.Framework.Input.Handlers.Keyboard;
using osu.Framework.Input.Handlers.Midi;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Input.Handlers.Pen;
using osu.Framework.Input.Handlers.Tablet;
using osu.Framework.Input.Handlers.Touch;
using osu.Framework.Platform.SDL2;
using osu.Framework.Platform.SDL3;
using SixLabors.ImageSharp.Formats.Png;

namespace osu.Framework.Platform
{
    public abstract class SDLGameHost : GameHost
    {
        public override bool CapsLockEnabled => Window is ISDLWindow { CapsLockPressed: true };

        public override bool OnScreenKeyboardOverlapsGameWindow => Window is ISDLWindow { KeyboardAttached: false };

        protected SDLGameHost(string gameName, HostOptions? options = null)
            : base(gameName, options)
        {
        }

        protected override TextInputSource CreateTextInput()
        {
            if (Window is ISDLWindow window)
                return new SDLWindowTextInput(window);

            return base.CreateTextInput();
        }

        protected override Clipboard CreateClipboard()
            => FrameworkEnvironment.UseSDL3
                ? new SDL3Clipboard(PngFormat.Instance) // PNG works well on linux
                : new SDL2Clipboard();

        protected override IEnumerable<InputHandler> CreateAvailableInputHandlers()
        {
            yield return new KeyboardHandler();

            // OpenTabletDriver pulls in HidSharp which probes macOS-only HID APIs at startup,
            // producing a noisy first-chance HidSharp.Platform.MacOS.NativeMethods exception on
            // Android. Tablet drivers are also not a meaningful input source on Android, so skip
            // the handler entirely on that platform.
            // tablet should get priority over mouse to correctly handle cases where tablet drivers report as mice as well.
            if (RuntimeInfo.OS != RuntimeInfo.Platform.Android)
                yield return new OpenTabletDriverHandler();

            // SDL3 pen events are not delivered on Android (stylus input arrives through the
            // touch path), and subscribing the handler still costs an InputHandler entry plus
            // per-frame event dispatch overhead. Skip on Android.
            if (FrameworkEnvironment.UseSDL3 && RuntimeInfo.OS != RuntimeInfo.Platform.Android)
                yield return new PenHandler();

            yield return new MouseHandler();
            yield return new TouchHandler();
            yield return new JoystickHandler();
            yield return new MidiHandler();
        }
    }
}
