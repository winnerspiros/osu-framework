// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics;

namespace osu.Framework.Extensions.Color4Extensions
{
    public static class Color4Extensions
    {
        public const double GAMMA = 2.4;

        // ToLinear is quite a hot path in the game.
        // MathF.Pow performs way faster than Math.Pow, however on Windows it lacks a fast path for x == 1.
        // Given passing color == 1 (White or Transparent) is very common, a fast path for 1 is added.

        public static double ToLinear(double color)
        {
            if (color == 1)
                return 1;

            return color <= 0.04045 ? color / 12.92 : Math.Pow((color + 0.055) / 1.055, GAMMA);
        }

        public static float ToLinear(float color)
        {
            if (color == 1)
                return 1;

            return color <= 0.04045f ? color / 12.92f : MathF.Pow((color + 0.055f) / 1.055f, (float)GAMMA);
        }

        public static double ToSRGB(double color)
        {
            if (color == 1)
                return 1;

            return color < 0.0031308 ? 12.92 * color : 1.055 * Math.Pow(color, 1.0 / GAMMA) - 0.055;
        }

        public static float ToSRGB(float color)
        {
            if (color == 1)
                return 1;

            return color < 0.0031308f ? 12.92f * color : 1.055f * MathF.Pow(color, 1.0f / (float)GAMMA) - 0.055f;
        }

        public static Colour4 Opacity(this Colour4 color, float a) => new Colour4(color.R, color.G, color.B, a);

        public static Colour4 Opacity(this Colour4 color, byte a) => new Colour4(color.R, color.G, color.B, a / 255f);

        public static Colour4 ToLinear(this Colour4 colour) =>
            new Colour4(
                ToLinear(colour.R),
                ToLinear(colour.G),
                ToLinear(colour.B),
                colour.A);

        public static Colour4 ToSRGB(this Colour4 colour) =>
            new Colour4(
                ToSRGB(colour.R),
                ToSRGB(colour.G),
                ToSRGB(colour.B),
                colour.A);

        public static Colour4 MultiplySRGB(Colour4 first, Colour4 second)
        {
            if (first.Equals(Colour4.White))
                return second;

            if (second.Equals(Colour4.White))
                return first;

            first = first.ToLinear();
            second = second.ToLinear();

            return new Colour4(
                first.R * second.R,
                first.G * second.G,
                first.B * second.B,
                first.A * second.A).ToSRGB();
        }

        public static Colour4 Multiply(Colour4 first, Colour4 second)
        {
            if (first.Equals(Colour4.White))
                return second;

            if (second.Equals(Colour4.White))
                return first;

            return new Colour4(
                first.R * second.R,
                first.G * second.G,
                first.B * second.B,
                first.A * second.A);
        }

        /// <summary>
        /// Returns a version of the color with negated components depending on arguments.
        /// Used for the shader-level additive blend mode.
        /// </summary>
        /// <param name="colour">Original colour</param>
        /// <param name="negateAlpha">Negates alpha if true</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Colour4 NegateAlphaIfTrue(this Colour4 colour, bool negateAlpha) =>
            new Colour4(colour.R, colour.G, colour.B, negateAlpha ? -colour.A : colour.A);

        /// <summary>
        /// Returns a lightened version of the colour.
        /// </summary>
        /// <param name="colour">Original colour</param>
        /// <param name="amount">Decimal light addition</param>
        public static Colour4 Lighten(this Colour4 colour, float amount) => Multiply(colour, 1 + amount);

        /// <summary>
        /// Returns a darkened version of the colour.
        /// </summary>
        /// <param name="colour">Original colour</param>
        /// <param name="amount">Percentage light reduction</param>
        public static Colour4 Darken(this Colour4 colour, float amount) => Multiply(colour, 1 / (1 + amount));

        /// <summary>
        /// Multiply the RGB coordinates by a scalar.
        /// </summary>
        /// <param name="colour">Original colour</param>
        /// <param name="scalar">A scalar to multiply with</param>
        public static Colour4 Multiply(this Colour4 colour, float scalar)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(scalar);

            return new Colour4(
                Math.Min(1, colour.R * scalar),
                Math.Min(1, colour.G * scalar),
                Math.Min(1, colour.B * scalar),
                colour.A);
        }

        /// <summary>
        /// Converts an RGB or RGBA-formatted hex colour code into a <see cref="Colour4"/>.
        /// Supported colour code formats:
        /// <list type="bullet">
        /// <item><description>RGB</description></item>
        /// <item><description>#RGB</description></item>
        /// <item><description>RGBA</description></item>
        /// <item><description>#RGBA</description></item>
        /// <item><description>RRGGBB</description></item>
        /// <item><description>#RRGGBB</description></item>
        /// <item><description>RRGGBBAA</description></item>
        /// <item><description>#RRGGBBAA</description></item>
        /// </list>
        /// </summary>
        /// <param name="hex">The hex code.</param>
        /// <returns>The <see cref="Colour4"/> representing the colour.</returns>
        /// <exception cref="ArgumentException">If <paramref name="hex"/> is not a supported colour code.</exception>
        public static Colour4 FromHex(string hex)
        {
            var hexSpan = hex[0] == '#' ? hex.AsSpan()[1..] : hex.AsSpan();

            switch (hexSpan.Length)
            {
                default:
                    throw new ArgumentException(@"Invalid hex string length!");

                case 3:
                    return new Colour4(
                        (byte)(byte.Parse(hexSpan[..1], NumberStyles.HexNumber) * 17),
                        (byte)(byte.Parse(hexSpan[1..2], NumberStyles.HexNumber) * 17),
                        (byte)(byte.Parse(hexSpan[2..3], NumberStyles.HexNumber) * 17),
                        255);

                case 6:
                    return new Colour4(
                        byte.Parse(hexSpan[..2], NumberStyles.HexNumber),
                        byte.Parse(hexSpan[2..4], NumberStyles.HexNumber),
                        byte.Parse(hexSpan[4..6], NumberStyles.HexNumber),
                        255);

                case 4:
                    return new Colour4(
                        (byte)(byte.Parse(hexSpan[..1], NumberStyles.HexNumber) * 17),
                        (byte)(byte.Parse(hexSpan[1..2], NumberStyles.HexNumber) * 17),
                        (byte)(byte.Parse(hexSpan[2..3], NumberStyles.HexNumber) * 17),
                        (byte)(byte.Parse(hexSpan[3..4], NumberStyles.HexNumber) * 17));

                case 8:
                    return new Colour4(
                        byte.Parse(hexSpan[..2], NumberStyles.HexNumber),
                        byte.Parse(hexSpan[2..4], NumberStyles.HexNumber),
                        byte.Parse(hexSpan[4..6], NumberStyles.HexNumber),
                        byte.Parse(hexSpan[6..8], NumberStyles.HexNumber));
            }
        }

        /// <summary>
        /// Converts a <see cref="Colour4"/> into a hex colour code.
        /// </summary>
        /// <param name="colour">The <see cref="Colour4"/> to convert.</param>
        /// <param name="alwaysOutputAlpha">Whether the alpha channel should always be output. If <c>false</c>, the alpha channel is only output if <paramref name="colour"/> is translucent.</param>
        /// <returns>The hex code representing the colour.</returns>
        public static string ToHex(this Colour4 colour, bool alwaysOutputAlpha = false)
        {
            int argb = (int)colour.ToARGB();
            byte a = (byte)(argb >> 24);
            byte r = (byte)(argb >> 16);
            byte g = (byte)(argb >> 8);
            byte b = (byte)argb;

            if (!alwaysOutputAlpha && a == 255)
                return $"#{r:X2}{g:X2}{b:X2}";

            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        /// <summary>
        /// Converts an HSV colour to a <see cref="Colour4"/>.
        /// </summary>
        /// <param name="h">The hue, between 0 and 360.</param>
        /// <param name="s">The saturation, between 0 and 1.</param>
        /// <param name="v">The value, between 0 and 1.</param>
        public static Colour4 FromHSV(float h, float s, float v)
        {
            if (h < 0 || h > 360)
                throw new ArgumentOutOfRangeException(nameof(h), "Hue must be between 0 and 360.");

            int hi = ((int)(h / 60.0f)) % 6;
            float f = h / 60.0f - (int)(h / 60.0);
            float p = v * (1 - s);
            float q = v * (1 - f * s);
            float t = v * (1 - (1 - f) * s);

            switch (hi)
            {
                case 0:
                    return toColor4(v, t, p);

                case 1:
                    return toColor4(q, v, p);

                case 2:
                    return toColor4(p, v, t);

                case 3:
                    return toColor4(p, q, v);

                case 4:
                    return toColor4(t, p, v);

                case 5:
                    return toColor4(v, p, q);

                default:
                    throw new ArgumentOutOfRangeException(nameof(h), "Hue is out of range.");
            }

            static Colour4 toColor4(float fr, float fg, float fb)
            {
                byte r = (byte)Math.Clamp(fr * 255, 0, 255);
                byte g = (byte)Math.Clamp(fg * 255, 0, 255);
                byte b = (byte)Math.Clamp(fb * 255, 0, 255);
                return new Colour4(r, g, b, 255);
            }
        }

        /// <summary>
        /// Converts a <see cref="Colour4"/> to an HSV colour.
        /// </summary>
        /// <param name="colour">The <see cref="Colour4"/> to convert.</param>
        /// <returns>The HSV colour.</returns>
        public static (float h, float s, float v) ToHSV(this Colour4 colour)
        {
            float h;
            float s;
            float r = colour.R;
            float g = colour.G;
            float b = colour.B;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));

            if (max == min)
                h = 0;
            else if (max == r)
                h = (60 * (g - b) / (max - min) + 360) % 360;
            else if (max == g)
                h = 60 * (b - r) / (max - min) + 120;
            else
                h = 60 * (r - g) / (max - min) + 240;

            if (max == 0)
                s = 0;
            else
                s = (max - min) / max;

            float v = max;

            return (h, s, v);
        }
    }
}
