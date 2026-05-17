// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Graphics.Rendering.Vertices
{
    /// <summary>
    /// Specifies the data type of each component in an array of vertex attributes.
    /// Values match the OpenGL ES3/GL4 <c>GL_*</c> constants.
    /// </summary>
    public enum VertexAttribPointerType
    {
        Byte = 0x1400,
        UnsignedByte = 0x1401,
        Short = 0x1402,
        UnsignedShort = 0x1403,
        Int = 0x1404,
        UnsignedInt = 0x1405,
        Float = 0x1406,
        HalfFloat = 0x140B,
    }
}
