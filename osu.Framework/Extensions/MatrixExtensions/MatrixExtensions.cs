// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;

namespace osu.Framework.Extensions.MatrixExtensions
{
    public static class MatrixExtensions
    {
        public static void TranslateFromLeft(ref Matrix3x2 m, Vector2 v)
        {
            m.M31 += m.M11 * v.X + m.M21 * v.Y;
            m.M32 += m.M12 * v.X + m.M22 * v.Y;
        }

        public static void TranslateFromRight(ref Matrix3x2 m, Vector2 v)
        {
            m.M31 += v.X;
            m.M32 += v.Y;
        }

        public static void RotateFromLeft(ref Matrix3x2 m, float radians)
        {
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);

            float m11 = m.M11 * cos + m.M21 * sin;
            float m12 = m.M12 * cos + m.M22 * sin;
            m.M21 = m.M21 * cos - m.M11 * sin;
            m.M22 = m.M22 * cos - m.M12 * sin;
            m.M11 = m11;
            m.M12 = m12;
        }

        public static void RotateFromRight(ref Matrix3x2 m, float radians)
        {
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);

            float m11 = m.M11 * cos - m.M12 * sin;
            float m21 = m.M21 * cos - m.M22 * sin;
            float m31 = m.M31 * cos - m.M32 * sin;

            m.M12 = m.M12 * cos + m.M11 * sin;
            m.M22 = m.M22 * cos + m.M21 * sin;
            m.M32 = m.M32 * cos + m.M31 * sin;

            m.M11 = m11;
            m.M21 = m21;
            m.M31 = m31;
        }

        public static void ScaleFromLeft(ref Matrix3x2 m, Vector2 v)
        {
            m.M11 *= v.X;
            m.M12 *= v.X;
            m.M21 *= v.Y;
            m.M22 *= v.Y;
        }

        public static void ScaleFromRight(ref Matrix3x2 m, Vector2 v)
        {
            m.M11 *= v.X;
            m.M21 *= v.X;
            m.M31 *= v.X;

            m.M12 *= v.Y;
            m.M22 *= v.Y;
            m.M32 *= v.Y;
        }

        /// <summary>
        /// Apply shearing in X and Y direction from the left hand side.
        /// Since shearing is non-commutative it is important to note that we
        /// first shear in the Y direction, and then in the X direction.
        /// </summary>
        /// <param name="m">The matrix to apply the shearing operation to.</param>
        /// <param name="v">The X and Y amounts of shearing.</param>
        public static void ShearFromLeft(ref Matrix3x2 m, Vector2 v)
        {
            float m11 = m.M11 + m.M21 * v.Y + m.M11 * v.X * v.Y;
            float m12 = m.M12 + m.M22 * v.Y + m.M12 * v.X * v.Y;
            m.M21 += m.M11 * v.X;
            m.M22 += m.M12 * v.X;
            m.M11 = m11;
            m.M12 = m12;
        }

        /// <summary>
        /// Apply shearing in X and Y direction from the right hand side.
        /// Since shearing is non-commutative it is important to note that we
        /// first shear in the X direction, and then in the Y direction.
        /// </summary>
        /// <param name="m">The matrix to apply the shearing operation to.</param>
        /// <param name="v">The X and Y amounts of shearing.</param>
        public static void ShearFromRight(ref Matrix3x2 m, Vector2 v)
        {
            float xy = v.X * v.Y;

            float m11 = m.M11 + m.M12 * v.X;
            float m21 = m.M21 + m.M22 * v.X;
            float m31 = m.M31 + m.M32 * v.X;

            m.M12 += m.M11 * v.Y + m.M12 * xy;
            m.M22 += m.M21 * v.Y + m.M22 * xy;
            m.M32 += m.M31 * v.Y + m.M32 * xy;

            m.M11 = m11;
            m.M21 = m21;
            m.M31 = m31;
        }

        public static void FastInvert(ref Matrix3x2 value)
        {
            if (!Matrix3x2.Invert(value, out value))
                value = default;
        }
    }
}
