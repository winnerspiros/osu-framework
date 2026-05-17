// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Numerics;
using System.Runtime.InteropServices;

namespace osu.Framework.Graphics.Shaders.Types
{
    /// <summary>
    /// Must be aligned to a 16-byte boundary.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public record struct UniformMatrix3
    {
        public UniformVector3 Row0;
        public UniformVector3 Row1;
        public UniformVector3 Row2;

        public static implicit operator UniformMatrix3(Matrix3x2 matrix) => new UniformMatrix3
        {
            Row0 = new Vector3(matrix.M11, matrix.M12, 0),
            Row1 = new Vector3(matrix.M21, matrix.M22, 0),
            Row2 = new Vector3(matrix.M31, matrix.M32, 1)
        };
    }
}
