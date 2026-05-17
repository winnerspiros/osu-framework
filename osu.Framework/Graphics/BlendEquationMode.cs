// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Graphics
{
    /// <summary>
    /// Specifies the blending equation for colour/alpha blend operations.
    /// Values match the OpenGL ES3/GL4 <c>GL_*</c> constants.
    /// </summary>
    public enum BlendEquationMode
    {
        FuncAdd = 0x8006,
        Min = 0x8007,
        Max = 0x8008,
        FuncSubtract = 0x800A,
        FuncReverseSubtract = 0x800B,
    }
}
