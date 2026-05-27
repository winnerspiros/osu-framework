// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;

namespace osu.Framework.Input
{
    /// <summary>
    /// Manages state events for a single key.
    /// </summary>
    public class KeyEventManager : ButtonEventManager<Key>
    {
        public KeyEventManager(Key key)
            : base(key)
        {
        }

        public void HandleRepeat(InputState state)
        {
            if (ButtonDownInputQueue == null)
                return;

            // Manual intersection avoids LINQ Intersect allocation on every repeat frame.
            repeatFilteredQueue.Clear();

            foreach (var drawable in ButtonDownInputQueue)
            {
                if (drawable.IsAlive && drawable.IsPresent && inputQueueContains(drawable))
                    repeatFilteredQueue.Add(drawable);
            }

            PropagateButtonEvent(repeatFilteredQueue, new KeyDownEvent(state, Button, true));
        }

        private readonly List<Drawable> repeatFilteredQueue = new List<Drawable>();

        private bool inputQueueContains(Drawable drawable)
        {
            foreach (var d in InputQueue)
            {
                if (ReferenceEquals(d, drawable))
                    return true;
            }

            return false;
        }

        protected override Drawable? HandleButtonDown(InputState state, List<Drawable> targets) => PropagateButtonEvent(targets, new KeyDownEvent(state, Button));

        protected override void HandleButtonUp(InputState state, List<Drawable> targets) =>
            PropagateButtonEvent(targets, new KeyUpEvent(state, Button));

        protected override bool SuppressLoggingEventInformation(Drawable drawable) => drawable is ICanSuppressKeyEventLogging canSuppress && canSuppress.SuppressKeyEventLogging;
    }
}
