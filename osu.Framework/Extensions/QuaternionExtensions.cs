// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using SNQuaternion = System.Numerics.Quaternion;
using TKQuaternion = osuTK.Quaternion;

namespace osu.Framework.Extensions
{
    public static class QuaternionExtensions
    {
        public static TKQuaternion ToOsuTK(this SNQuaternion q) =>
            new TKQuaternion(q.X, q.Y, q.Z, q.W);

        public static SNQuaternion ToSystemNumerics(this TKQuaternion q) =>
            new SNQuaternion(q.X, q.Y, q.Z, q.W);
    }
}
