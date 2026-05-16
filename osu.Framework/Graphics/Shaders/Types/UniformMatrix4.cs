// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.InteropServices;
using osu.Framework.Extensions;
using osuTK;

namespace osu.Framework.Graphics.Shaders.Types
{
    /// <summary>
    /// Must be aligned to a 16-byte boundary.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public record struct UniformMatrix4
    {
        public UniformVector4 Row0;
        public UniformVector4 Row1;
        public UniformVector4 Row2;
        public UniformVector4 Row3;

        public static implicit operator Matrix4(UniformMatrix4 matrix) => new Matrix4
        {
            Row0 = ((System.Numerics.Vector4)matrix.Row0).ToOsuTK(),
            Row1 = ((System.Numerics.Vector4)matrix.Row1).ToOsuTK(),
            Row2 = ((System.Numerics.Vector4)matrix.Row2).ToOsuTK(),
            Row3 = ((System.Numerics.Vector4)matrix.Row3).ToOsuTK()
        };

        public static implicit operator UniformMatrix4(Matrix4 matrix) => new UniformMatrix4
        {
            Row0 = matrix.Row0.ToSystemNumerics(),
            Row1 = matrix.Row1.ToSystemNumerics(),
            Row2 = matrix.Row2.ToSystemNumerics(),
            Row3 = matrix.Row3.ToSystemNumerics()
        };
    }
}
