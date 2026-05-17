// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using SDPoint = System.Drawing.Point;
using SDSize = System.Drawing.Size;
using SNVector2 = System.Numerics.Vector2;

namespace osu.Framework.Extensions
{
    /// <summary>
    /// Extension functions for bridging between System.Drawing and System.Numerics.
    /// </summary>
    public static class BridgingExtensions
    {
        public static SNVector2 ToSystemNumerics(this SDSize size) =>
            new SNVector2(size.Width, size.Height);

        public static SNVector2 ToSystemNumerics(this SDPoint point) =>
            new SNVector2(point.X, point.Y);

        public static SDSize ToSystemDrawingSize(this SNVector2 vec) =>
            new SDSize((int)vec.X, (int)vec.Y);

        public static SDPoint ToSystemDrawingPoint(this SNVector2 vec) =>
            new SDPoint((int)vec.X, (int)vec.Y);
    }
}
