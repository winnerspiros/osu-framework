// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace osu.Framework.Testing.Drawables.Steps
{
    public abstract partial class StepButton : CompositeDrawable, IHasTooltip
    {
        public required bool IsSetupStep { get; init; }
        public Action? Action { get; set; }

        public virtual int RequiredRepetitions => 1;

        protected virtual Colour4 IdleColour => new Colour4(0.15f, 0.15f, 0.15f, 1);
        protected virtual Colour4 RunningColour => new Colour4(0.5f, 0.5f, 0.5f, 1);

        protected readonly Box Light;
        protected readonly Box Background;
        protected readonly SpriteText SpriteText;

        protected StepButton()
        {
            InternalChildren = new Drawable[]
            {
                Background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = IdleColour,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                Light = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 5,
                },
                SpriteText = new SpriteText
                {
                    Truncate = true,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    Font = FrameworkFont.Regular.With(size: 14),
                    X = 5,
                    Padding = new MarginPadding(5)
                }
            };

            Height = 20;
            RelativeSizeAxes = Axes.X;

            BorderThickness = 1.5f;
            BorderColour = new Colour4(0.15f, 0.15f, 0.15f, 1);

            Masking = true;
        }

        public LocalisableString Text
        {
            get => SpriteText.Text;
            set => SpriteText.Text = value;
        }

        public Colour4 LightColour
        {
            get;
            set
            {
                field = value;
                if (IsLoaded) Reset();
            }
        } = Colour4.BlueViolet;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Reset();
        }

        protected override bool OnClick(ClickEvent e)
        {
            try
            {
                PerformStep(true);
            }
            catch (Exception exc)
            {
                Logging.Logger.Error(exc, $"Step {this} triggered an error");
            }

            return true;
        }

        /// <summary>
        /// Reset this step to a default state.
        /// </summary>
        public virtual void Reset()
        {
            Background.DelayUntilTransformsFinished().FadeColour(IdleColour, 1000, Easing.OutQuint);
            Light.FadeColour(LightColour);
        }

        public virtual void PerformStep(bool userTriggered = false)
        {
            Background.ClearTransforms();
            Background.FadeColour(RunningColour, 400, Easing.OutQuint);

            try
            {
                Action?.Invoke();
            }
            catch (Exception)
            {
                Failure();
                throw;
            }
        }

        protected virtual void Failure()
        {
            Background.DelayUntilTransformsFinished().FadeColour(new Colour4(0.3f, 0.15f, 0.15f, 1), 1000, Easing.OutQuint);
            Light.FadeColour(Colour4.Red);
        }

        protected virtual void Success()
        {
            Background.FinishTransforms();
            Background.FadeColour(IdleColour, 1000, Easing.OutQuint);

            Light.FadeColour(Colour4.YellowGreen);
        }

        public override string ToString() => $@"""{Text}"" " + base.ToString();

        LocalisableString IHasTooltip.TooltipText => SpriteText.IsTruncated ? Text : string.Empty;
    }
}
