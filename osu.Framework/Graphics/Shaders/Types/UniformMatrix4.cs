// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Numerics;
using System.Runtime.InteropServices;

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

        public static implicit operator Matrix4x4(UniformMatrix4 matrix) => new Matrix4x4(
            matrix.Row0.X, matrix.Row0.Y, matrix.Row0.Z, matrix.Row0.W,
            matrix.Row1.X, matrix.Row1.Y, matrix.Row1.Z, matrix.Row1.W,
            matrix.Row2.X, matrix.Row2.Y, matrix.Row2.Z, matrix.Row2.W,
            matrix.Row3.X, matrix.Row3.Y, matrix.Row3.Z, matrix.Row3.W);

        public static implicit operator UniformMatrix4(Matrix4x4 matrix) => new UniformMatrix4
        {
            Row0 = new Vector4(matrix.M11, matrix.M12, matrix.M13, matrix.M14),
            Row1 = new Vector4(matrix.M21, matrix.M22, matrix.M23, matrix.M24),
            Row2 = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34),
            Row3 = new Vector4(matrix.M41, matrix.M42, matrix.M43, matrix.M44)
        };
    }
}
