// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using System.Text;
using osu.Framework.Platform;
using Veldrid.OpenGL;

namespace osu.Framework.Graphics.OpenGL
{
    public enum StringName : uint
    {
        Vendor = 0x1F00,
        Renderer = 0x1F01,
        Version = 0x1F02,
        ShadingLanguageVersion = 0x8B8C,
    }

    public enum StringNameIndexed : uint
    {
        Extensions = 0x1F03,
    }

    public enum GetPName : uint
    {
        MaxTextureSize = 0x0D33,
        MaxRenderbufferSize = 0x8D57,
    }

    [Obsolete("Use typed enums instead.")]
    public enum All : uint
    {
        Nearest = 0x2600,
        Linear = 0x2601,
        LinearMipmapLinear = 0x2703,
        NumExtensions = 0x821D,
    }

    public enum EnableCap : uint
    {
        Blend = 0x0BE2,
        DepthTest = 0x0B71,
        StencilTest = 0x0B90,
        ScissorTest = 0x0C11,
    }

    public enum TextureUnit : uint
    {
        Texture0 = 0x84C0,
    }

    public enum TextureTarget : uint
    {
        Texture2D = 0x0DE1,
    }

    public enum TextureTarget2d : uint
    {
        Texture2D = 0x0DE1,
    }

    public enum TextureComponentCount : int
    {
        R8 = 0x8229,
        Rg8 = 0x822B,
        Rgb8 = 0x8051,
        Rgba8 = 0x8058,
    }

    public enum TextureParameterName : uint
    {
        TextureMagFilter = 0x2800,
        TextureMinFilter = 0x2801,
        TextureWrapS = 0x2802,
        TextureWrapT = 0x2803,
        TextureBaseLevel = 0x813C,
        TextureMaxLevel = 0x813D,
        TextureMinLod = 0x813A,
        TextureMaxLod = 0x813B,
    }

    public enum TextureWrapMode : int
    {
        Repeat = 0x2901,
        ClampToEdge = 0x812F,
        MirroredRepeat = 0x8370,
    }

    public enum PixelFormat : uint
    {
        Red = 0x1903,
        Rgba = 0x1908,
    }

    public enum PixelType : uint
    {
        UnsignedByte = 0x1401,
    }

    [Flags]
    public enum ClearBufferMask : uint
    {
        DepthBufferBit = 0x0100,
        StencilBufferBit = 0x0400,
        ColorBufferBit = 0x4000,
    }

    public enum BufferTarget : uint
    {
        ArrayBuffer = 0x8892,
        ElementArrayBuffer = 0x8893,
        UniformBuffer = 0x8A11,
    }

    public enum BufferUsageHint : uint
    {
        StreamDraw = 0x88E0,
        StaticDraw = 0x88E4,
        DynamicDraw = 0x88E8,
    }

    public enum BufferRangeTarget : uint
    {
        UniformBuffer = 0x8A11,
        ShaderStorageBuffer = 0x90D2,
    }

    public enum FramebufferTarget : uint
    {
        Framebuffer = 0x8D40,
    }

    public enum FramebufferAttachment : uint
    {
        ColorAttachment0 = 0x8CE0,
        DepthAttachment = 0x8D00,
        StencilAttachment = 0x8D20,
        DepthStencilAttachment = 0x821A,
    }

    public enum RenderbufferTarget : uint
    {
        Renderbuffer = 0x8D41,
    }

    public enum RenderbufferInternalFormat : uint
    {
        R8 = 0x8229,
        R8Snorm = 0x8F94,
        R16f = 0x822D,
        R32f = 0x822E,
        R8ui = 0x8232,
        R8i = 0x8231,
        R16ui = 0x8234,
        R16i = 0x8233,
        R32ui = 0x8236,
        R32i = 0x8235,
        Rg8 = 0x822B,
        Rg8Snorm = 0x8F95,
        Rg16f = 0x822F,
        Rg32f = 0x8230,
        Rg8ui = 0x8238,
        Rg8i = 0x8237,
        Rg16ui = 0x823A,
        Rg16i = 0x8239,
        Rg32ui = 0x823C,
        Rg32i = 0x823B,
        Rgb8 = 0x8051,
        Srgb8 = 0x8C41,
        Rgb565 = 0x8D62,
        Rgb8Snorm = 0x8F96,
        R11fG11fB10f = 0x8C3A,
        Rgb9E5 = 0x8C3D,
        Rgb16f = 0x881B,
        Rgb32f = 0x8815,
        Rgb8ui = 0x8D7D,
        Rgb8i = 0x8D8F,
        Rgb16ui = 0x8D77,
        Rgb16i = 0x8D89,
        Rgb32ui = 0x8D71,
        Rgb32i = 0x8D83,
        Rgba8 = 0x8058,
        Srgb8Alpha8 = 0x8C43,
        Rgba8Snorm = 0x8F97,
        Rgb5A1 = 0x8057,
        Rgba4 = 0x8056,
        Rgb10A2 = 0x8059,
        Rgba16f = 0x881A,
        Rgba32f = 0x8814,
        Rgba8i = 0x8D8E,
        Rgba8ui = 0x8D7C,
        Rgb10A2ui = 0x906F,
        Rgba16i = 0x8D88,
        Rgba16ui = 0x8D76,
        Rgba32i = 0x8D82,
        Rgba32ui = 0x8D70,
        DepthComponent16 = 0x81A5,
        DepthComponent24 = 0x81A6,
        DepthComponent32f = 0x8CAC,
        Depth24Stencil8 = 0x88F0,
        Depth32fStencil8 = 0x8CAD,
        StencilIndex8 = 0x8D48,
    }

    public enum DrawElementsType : uint
    {
        UnsignedShort = 0x1403,
        UnsignedInt = 0x1405,
    }

    public enum PrimitiveType : uint
    {
        Points = 0,
        Lines = 1,
        LineStrip = 3,
        Triangles = 4,
        TriangleStrip = 5,
    }

    public enum ShaderType : uint
    {
        FragmentShader = 0x8B30,
        VertexShader = 0x8B31,
    }

    public enum ShaderParameter : uint
    {
        CompileStatus = 0x8B81,
        InfoLogLength = 0x8B84,
    }

    public enum GetProgramParameterName : uint
    {
        LinkStatus = 0x8B82,
    }

    public enum BlendEquationMode : uint
    {
        FuncAdd = 0x8006,
        Min = 0x8007,
        Max = 0x8008,
        FuncSubtract = 0x800A,
        FuncReverseSubtract = 0x800B,
    }

    public enum BlendingFactorSrc : uint
    {
        Zero = 0,
        One = 1,
        SrcColor = 0x0300,
        OneMinusSrcColor = 0x0301,
        SrcAlpha = 0x0302,
        OneMinusSrcAlpha = 0x0303,
        DstAlpha = 0x0304,
        OneMinusDstAlpha = 0x0305,
        DstColor = 0x0306,
        OneMinusDstColor = 0x0307,
        SrcAlphaSaturate = 0x0308,
        ConstantColor = 0x8001,
        OneMinusConstantColor = 0x8002,
        ConstantAlpha = 0x8003,
        OneMinusConstantAlpha = 0x8004,
    }

    public enum BlendingFactorDest : uint
    {
        Zero = 0,
        One = 1,
        SrcColor = 0x0300,
        OneMinusSrcColor = 0x0301,
        SrcAlpha = 0x0302,
        OneMinusSrcAlpha = 0x0303,
        DstAlpha = 0x0304,
        OneMinusDstAlpha = 0x0305,
        DstColor = 0x0306,
        OneMinusDstColor = 0x0307,
        SrcAlphaSaturate = 0x0308,
        ConstantColor = 0x8001,
        OneMinusConstantColor = 0x8002,
        ConstantAlpha = 0x8003,
        OneMinusConstantAlpha = 0x8004,
    }

    public enum DepthFunction : uint
    {
        Never = 0x0200,
        Less = 0x0201,
        Equal = 0x0202,
        Lequal = 0x0203,
        Greater = 0x0204,
        Notequal = 0x0205,
        Gequal = 0x0206,
        Always = 0x0207,
    }

    public enum StencilFunction : uint
    {
        Never = 0x0200,
        Less = 0x0201,
        Equal = 0x0202,
        Lequal = 0x0203,
        Greater = 0x0204,
        Notequal = 0x0205,
        Gequal = 0x0206,
        Always = 0x0207,
    }

    public enum StencilOp : uint
    {
        Zero = 0,
        Invert = 0x150A,
        Keep = 0x1E00,
        Replace = 0x1E01,
        Incr = 0x1E02,
        Decr = 0x1E03,
        IncrWrap = 0x8507,
        DecrWrap = 0x8508,
    }

    public enum VertexAttribPointerType : uint
    {
        Byte = 0x1400,
        UnsignedByte = 0x1401,
        Short = 0x1402,
        UnsignedShort = 0x1403,
        Int = 0x1404,
        UnsignedInt = 0x1405,
        Float = 0x1406,
        Double = 0x140A,
        HalfFloat = 0x140B,
    }

    public enum VertexAttribIntegerType : uint
    {
        Byte = 0x1400,
        UnsignedByte = 0x1401,
        Short = 0x1402,
        UnsignedShort = 0x1403,
        Int = 0x1404,
        UnsignedInt = 0x1405,
    }

    public enum HintTarget : uint
    {
        GenerateMipmapHint = 0x8192,
    }

    public enum HintMode : uint
    {
        Nicest = 0x1102,
    }

    public enum PixelStoreParameter : uint
    {
        UnpackAlignment = 0x0CF5,
        PackAlignment = 0x0D05,
        UnpackRowLength = 0x0CF2,
    }

    public enum ProgramInterface : uint
    {
        Uniform = 0x92E1,
        UniformBlock = 0x92E2,
        ShaderStorageBlock = 0x92E6,
    }

    internal static unsafe class GL
    {
        internal static OpenGLProcTable Table;

        private static delegate* unmanaged<uint, uint, byte*> _getStringi;
        private static delegate* unmanaged<int, void> _clearStencil;
        private static delegate* unmanaged<int, float, float, void> _uniform2f;
        private static delegate* unmanaged<int, float, float, float, void> _uniform3f;
        private static delegate* unmanaged<int, float, float, float, float, void> _uniform4f;

        internal static void Initialise(IOpenGLGraphicsSurface surface)
        {
            Table = new OpenGLProcTable
            {
                ActiveTexture = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glActiveTexture"),
                BindTexture = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glBindTexture"),
                GenTextures = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glGenTextures"),
                DeleteTextures = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glDeleteTextures"),
                TexImage2D = (delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)(void*)surface.GetProcAddress("glTexImage2D"),
                TexSubImage2D = (delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)(void*)surface.GetProcAddress("glTexSubImage2D"),
                TexParameteri = (delegate* unmanaged<uint, uint, int, void>)(void*)surface.GetProcAddress("glTexParameteri"),
                GenerateMipmap = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glGenerateMipmap"),
                BindFramebuffer = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glBindFramebuffer"),
                GenFramebuffers = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glGenFramebuffers"),
                DeleteFramebuffers = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glDeleteFramebuffers"),
                FramebufferTexture2D = (delegate* unmanaged<uint, uint, uint, uint, int, void>)(void*)surface.GetProcAddress("glFramebufferTexture2D"),
                FramebufferRenderbuffer = (delegate* unmanaged<uint, uint, uint, uint, void>)(void*)surface.GetProcAddress("glFramebufferRenderbuffer"),
                InvalidateFramebuffer = (delegate* unmanaged<uint, int, uint*, void>)(void*)surface.GetProcAddress("glInvalidateFramebuffer"),
                BindRenderbuffer = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glBindRenderbuffer"),
                GenRenderbuffers = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glGenRenderbuffers"),
                DeleteRenderbuffers = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glDeleteRenderbuffers"),
                RenderbufferStorage = (delegate* unmanaged<uint, uint, int, int, void>)(void*)surface.GetProcAddress("glRenderbufferStorage"),
                BindVertexArray = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glBindVertexArray"),
                GenVertexArrays = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glGenVertexArrays"),
                DeleteVertexArrays = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glDeleteVertexArrays"),
                EnableVertexAttribArray = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glEnableVertexAttribArray"),
                VertexAttribPointer = (delegate* unmanaged<uint, int, uint, byte, uint, void*, void>)(void*)surface.GetProcAddress("glVertexAttribPointer"),
                VertexAttribIPointer = (delegate* unmanaged<uint, int, uint, uint, void*, void>)(void*)surface.GetProcAddress("glVertexAttribIPointer"),
                BindBuffer = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glBindBuffer"),
                BindBufferBase = (delegate* unmanaged<uint, uint, uint, void>)(void*)surface.GetProcAddress("glBindBufferBase"),
                GenBuffers = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glGenBuffers"),
                DeleteBuffers = (delegate* unmanaged<int, uint*, void>)(void*)surface.GetProcAddress("glDeleteBuffers"),
                BufferData = (delegate* unmanaged<uint, UIntPtr, void*, uint, void>)(void*)surface.GetProcAddress("glBufferData"),
                BufferSubData = (delegate* unmanaged<uint, IntPtr, UIntPtr, void*, void>)(void*)surface.GetProcAddress("glBufferSubData"),
                CreateShader = (delegate* unmanaged<uint, uint>)(void*)surface.GetProcAddress("glCreateShader"),
                ShaderSource = (delegate* unmanaged<uint, int, byte**, int*, void>)(void*)surface.GetProcAddress("glShaderSource"),
                CompileShader = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glCompileShader"),
                GetShaderiv = (delegate* unmanaged<uint, uint, int*, void>)(void*)surface.GetProcAddress("glGetShaderiv"),
                GetShaderInfoLog = (delegate* unmanaged<uint, int, int*, byte*, void>)(void*)surface.GetProcAddress("glGetShaderInfoLog"),
                DeleteShader = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glDeleteShader"),
                CreateProgram = (delegate* unmanaged<uint>)(void*)surface.GetProcAddress("glCreateProgram"),
                AttachShader = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glAttachShader"),
                DetachShader = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glDetachShader"),
                LinkProgram = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glLinkProgram"),
                GetProgramiv = (delegate* unmanaged<uint, uint, int*, void>)(void*)surface.GetProcAddress("glGetProgramiv"),
                GetProgramInfoLog = (delegate* unmanaged<uint, int, int*, byte*, void>)(void*)surface.GetProcAddress("glGetProgramInfoLog"),
                DeleteProgram = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glDeleteProgram"),
                UseProgram = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glUseProgram"),
                GetUniformLocation = (delegate* unmanaged<uint, byte*, int>)(void*)surface.GetProcAddress("glGetUniformLocation"),
                GetUniformBlockIndex = (delegate* unmanaged<uint, byte*, uint>)(void*)surface.GetProcAddress("glGetUniformBlockIndex"),
                UniformBlockBinding = (delegate* unmanaged<uint, uint, uint, void>)(void*)surface.GetProcAddress("glUniformBlockBinding"),
                Uniform1i = (delegate* unmanaged<int, int, void>)(void*)surface.GetProcAddress("glUniform1i"),
                Uniform1f = (delegate* unmanaged<int, float, void>)(void*)surface.GetProcAddress("glUniform1f"),
                UniformMatrix3fv = (delegate* unmanaged<int, int, byte, float*, void>)(void*)surface.GetProcAddress("glUniformMatrix3fv"),
                UniformMatrix4fv = (delegate* unmanaged<int, int, byte, float*, void>)(void*)surface.GetProcAddress("glUniformMatrix4fv"),
                ShaderStorageBlockBinding = (delegate* unmanaged<uint, uint, uint, void>)(void*)surface.GetProcAddress("glShaderStorageBlockBinding"),
                GetProgramResourceIndex = (delegate* unmanaged<uint, uint, byte*, uint>)(void*)surface.GetProcAddress("glGetProgramResourceIndex"),
                DrawElements = (delegate* unmanaged<uint, int, uint, void*, void>)(void*)surface.GetProcAddress("glDrawElements"),
                Clear = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glClear"),
                ClearColor = (delegate* unmanaged<float, float, float, float, void>)(void*)surface.GetProcAddress("glClearColor"),
                ClearDepth = (delegate* unmanaged<double, void>)(void*)surface.GetProcAddress("glClearDepth"),
                ClearDepthF = (delegate* unmanaged<float, void>)(void*)surface.GetProcAddress("glClearDepthf"),
                Enable = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glEnable"),
                Disable = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glDisable"),
                ColorMask = (delegate* unmanaged<byte, byte, byte, byte, void>)(void*)surface.GetProcAddress("glColorMask"),
                DepthFunc = (delegate* unmanaged<uint, void>)(void*)surface.GetProcAddress("glDepthFunc"),
                DepthMask = (delegate* unmanaged<byte, void>)(void*)surface.GetProcAddress("glDepthMask"),
                BlendFuncSeparate = (delegate* unmanaged<uint, uint, uint, uint, void>)(void*)surface.GetProcAddress("glBlendFuncSeparate"),
                BlendEquationSeparate = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glBlendEquationSeparate"),
                StencilFunc = (delegate* unmanaged<uint, int, uint, void>)(void*)surface.GetProcAddress("glStencilFunc"),
                StencilOp = (delegate* unmanaged<uint, uint, uint, void>)(void*)surface.GetProcAddress("glStencilOp"),
                Viewport = (delegate* unmanaged<int, int, int, int, void>)(void*)surface.GetProcAddress("glViewport"),
                Scissor = (delegate* unmanaged<int, int, int, int, void>)(void*)surface.GetProcAddress("glScissor"),
                PixelStorei = (delegate* unmanaged<uint, int, void>)(void*)surface.GetProcAddress("glPixelStorei"),
                ReadPixels = (delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)(void*)surface.GetProcAddress("glReadPixels"),
                GetIntegerv = (delegate* unmanaged<uint, int*, void>)(void*)surface.GetProcAddress("glGetIntegerv"),
                GetString = (delegate* unmanaged<uint, byte*>)(void*)surface.GetProcAddress("glGetString"),
                Hint = (delegate* unmanaged<uint, uint, void>)(void*)surface.GetProcAddress("glHint"),
                Finish = (delegate* unmanaged<void>)(void*)surface.GetProcAddress("glFinish"),
            };

            _getStringi = (delegate* unmanaged<uint, uint, byte*>)(void*)surface.GetProcAddress("glGetStringi");
            _clearStencil = (delegate* unmanaged<int, void>)(void*)surface.GetProcAddress("glClearStencil");
            _uniform2f = (delegate* unmanaged<int, float, float, void>)(void*)surface.GetProcAddress("glUniform2f");
            _uniform3f = (delegate* unmanaged<int, float, float, float, void>)(void*)surface.GetProcAddress("glUniform3f");
            _uniform4f = (delegate* unmanaged<int, float, float, float, float, void>)(void*)surface.GetProcAddress("glUniform4f");
        }

        public static string GetString(StringName name)
        {
            byte* ptr = Table.GetString((uint)name);
            return ptr == null ? string.Empty : Marshal.PtrToStringAnsi((IntPtr)ptr) ?? string.Empty;
        }

        public static string GetString(StringNameIndexed name, int index)
        {
            byte* ptr = _getStringi((uint)name, (uint)index);
            return ptr == null ? string.Empty : Marshal.PtrToStringAnsi((IntPtr)ptr) ?? string.Empty;
        }

        public static int GetInteger(GetPName pname)
        {
            int value = 0;
            Table.GetIntegerv((uint)pname, &value);
            return value;
        }

#pragma warning disable CS0618
        public static void GetInteger(All pname, out int data)
        {
            int value = 0;
            Table.GetIntegerv((uint)pname, &value);
            data = value;
        }
#pragma warning restore CS0618

        public static void Enable(EnableCap cap) => Table.Enable((uint)cap);
        public static void Disable(EnableCap cap) => Table.Disable((uint)cap);

        public static void ColorMask(bool red, bool green, bool blue, bool alpha)
            => Table.ColorMask(red ? (byte)1 : (byte)0, green ? (byte)1 : (byte)0, blue ? (byte)1 : (byte)0, alpha ? (byte)1 : (byte)0);

        public static void DepthMask(bool flag) => Table.DepthMask(flag ? (byte)1 : (byte)0);
        public static void DepthFunc(DepthFunction func) => Table.DepthFunc((uint)func);

        public static void BlendEquationSeparate(BlendEquationMode modeRGB, BlendEquationMode modeAlpha)
            => Table.BlendEquationSeparate((uint)modeRGB, (uint)modeAlpha);

        public static void BlendFuncSeparate(BlendingFactorSrc srcRGB, BlendingFactorDest dstRGB, BlendingFactorSrc srcAlpha, BlendingFactorDest dstAlpha)
            => Table.BlendFuncSeparate((uint)srcRGB, (uint)dstRGB, (uint)srcAlpha, (uint)dstAlpha);

        public static void StencilFunc(StencilFunction func, int @ref, uint mask)
            => Table.StencilFunc((uint)func, @ref, mask);

        public static void StencilOp(StencilOp sfail, StencilOp dpfail, StencilOp dppass)
            => Table.StencilOp((uint)sfail, (uint)dpfail, (uint)dppass);

        public static void Clear(ClearBufferMask mask) => Table.Clear((uint)mask);

        public static void ClearColor(Colour4 colour) => Table.ClearColor(colour.R, colour.G, colour.B, colour.A);

        public static void ClearDepth(float depth) => Table.ClearDepthF(depth);

        public static void ClearStencil(int s) => _clearStencil(s);

        public static void Viewport(int x, int y, int width, int height) => Table.Viewport(x, y, width, height);
        public static void Scissor(int x, int y, int width, int height) => Table.Scissor(x, y, width, height);

        public static void Finish() => Table.Finish();

        public static void BindVertexArray(int array) => Table.BindVertexArray((uint)array);

        public static int GenVertexArray()
        {
            uint id;
            Table.GenVertexArrays(1, &id);
            return (int)id;
        }

        public static void DeleteVertexArray(int array)
        {
            uint id = (uint)array;
            Table.DeleteVertexArrays(1, &id);
        }

        public static void EnableVertexAttribArray(int index) => Table.EnableVertexAttribArray((uint)index);

        public static void VertexAttribPointer(int index, int size, VertexAttribPointerType type, bool normalized, int stride, IntPtr pointer)
            => Table.VertexAttribPointer((uint)index, size, (uint)type, normalized ? (byte)1 : (byte)0, (uint)stride, (void*)pointer);

        public static void VertexAttribIPointer(int index, int size, VertexAttribIntegerType type, int stride, IntPtr pointer)
            => Table.VertexAttribIPointer((uint)index, size, (uint)type, (uint)stride, (void*)pointer);

        public static int GenBuffer()
        {
            uint id;
            Table.GenBuffers(1, &id);
            return (int)id;
        }

        public static void DeleteBuffer(int buffer)
        {
            uint id = (uint)buffer;
            Table.DeleteBuffers(1, &id);
        }

        public static void BindBuffer(BufferTarget target, int buffer) => Table.BindBuffer((uint)target, (uint)buffer);

        public static void BufferData<T>(BufferTarget target, nint size, ref T data, BufferUsageHint usage) where T : unmanaged
        {
            fixed (T* p = &data)
                Table.BufferData((uint)target, (UIntPtr)size, p, (uint)usage);
        }

        public static void BufferData<T>(BufferTarget target, nint size, T[] data, BufferUsageHint usage) where T : unmanaged
        {
            fixed (T* p = data)
                Table.BufferData((uint)target, (UIntPtr)size, p, (uint)usage);
        }

        public static void BufferSubData<T>(BufferTarget target, nint offset, nint size, ref T data) where T : unmanaged
        {
            fixed (T* p = &data)
                Table.BufferSubData((uint)target, (IntPtr)offset, (UIntPtr)size, p);
        }

        public static void BindBufferBase(BufferRangeTarget target, int index, int buffer)
            => Table.BindBufferBase((uint)target, (uint)index, (uint)buffer);

        public static void DrawElements(PrimitiveType mode, int count, DrawElementsType type, int offset)
            => Table.DrawElements((uint)mode, count, (uint)type, (void*)offset);

        public static void ActiveTexture(TextureUnit texture) => Table.ActiveTexture((uint)texture);

        public static void BindTexture(TextureTarget target, int texture) => Table.BindTexture((uint)target, (uint)texture);

        public static void GenTextures(int n, out int textures)
        {
            uint id;
            Table.GenTextures(n, &id);
            textures = (int)id;
        }

        public static void GenTextures(int n, int[] textures)
        {
            fixed (int* p = textures)
                Table.GenTextures(n, (uint*)p);
        }

        public static void DeleteTextures(int n, ref int textures)
        {
            fixed (int* p = &textures)
                Table.DeleteTextures(n, (uint*)p);
        }

        public static void TexImage2D(TextureTarget2d target, int level, TextureComponentCount internalformat, int width, int height, int border, PixelFormat format, PixelType type, IntPtr pixels)
            => Table.TexImage2D((uint)target, level, (int)internalformat, width, height, border, (uint)format, (uint)type, (void*)pixels);

        public static void TexImage2D<T>(TextureTarget2d target, int level, TextureComponentCount internalformat, int width, int height, int border, PixelFormat format, PixelType type, ref T pixels) where T : unmanaged
        {
            fixed (T* p = &pixels)
                Table.TexImage2D((uint)target, level, (int)internalformat, width, height, border, (uint)format, (uint)type, p);
        }

        public static void TexSubImage2D(TextureTarget2d target, int level, int xoffset, int yoffset, int width, int height, PixelFormat format, PixelType type, IntPtr pixels)
            => Table.TexSubImage2D((uint)target, level, xoffset, yoffset, width, height, (uint)format, (uint)type, (void*)pixels);

        public static void TexParameter(TextureTarget target, TextureParameterName pname, int param)
            => Table.TexParameteri((uint)target, (uint)pname, param);

        public static void GenerateMipmap(TextureTarget target) => Table.GenerateMipmap((uint)target);

        public static void Hint(HintTarget target, HintMode mode) => Table.Hint((uint)target, (uint)mode);

        public static void PixelStore(PixelStoreParameter pname, int param) => Table.PixelStorei((uint)pname, param);

        public static void ReadPixels<T>(int x, int y, int width, int height, PixelFormat format, PixelType type, ref T pixels) where T : unmanaged
        {
            fixed (T* p = &pixels)
                Table.ReadPixels(x, y, width, height, (uint)format, (uint)type, p);
        }

        public static int GenFramebuffer()
        {
            uint id;
            Table.GenFramebuffers(1, &id);
            return (int)id;
        }

        public static void DeleteFramebuffer(int framebuffer)
        {
            uint id = (uint)framebuffer;
            Table.DeleteFramebuffers(1, &id);
        }

        public static void BindFramebuffer(FramebufferTarget target, int framebuffer) => Table.BindFramebuffer((uint)target, (uint)framebuffer);

        public static void FramebufferTexture2D(FramebufferTarget target, FramebufferAttachment attachment, TextureTarget2d textarget, int texture, int level)
            => Table.FramebufferTexture2D((uint)target, (uint)attachment, (uint)textarget, (uint)texture, level);

        public static void FramebufferRenderbuffer(FramebufferTarget target, FramebufferAttachment attachment, RenderbufferTarget renderbuffertarget, int renderbuffer)
            => Table.FramebufferRenderbuffer((uint)target, (uint)attachment, (uint)renderbuffertarget, (uint)renderbuffer);

        public static void InvalidateFramebuffer(FramebufferTarget target, int numAttachments, ref FramebufferAttachment attachments)
        {
            if (Table.InvalidateFramebuffer == null)
                return;

            fixed (FramebufferAttachment* p = &attachments)
                Table.InvalidateFramebuffer((uint)target, numAttachments, (uint*)p);
        }

        public static int GenRenderbuffer()
        {
            uint id;
            Table.GenRenderbuffers(1, &id);
            return (int)id;
        }

        public static void DeleteRenderbuffer(int renderbuffer)
        {
            uint id = (uint)renderbuffer;
            Table.DeleteRenderbuffers(1, &id);
        }

        public static void BindRenderbuffer(RenderbufferTarget target, int renderbuffer) => Table.BindRenderbuffer((uint)target, (uint)renderbuffer);

        public static void RenderbufferStorage(RenderbufferTarget target, RenderbufferInternalFormat internalformat, int width, int height)
            => Table.RenderbufferStorage((uint)target, (uint)internalformat, width, height);

        public static int CreateShader(ShaderType type) => (int)Table.CreateShader((uint)type);

        public static void ShaderSource(int shader, string source)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(source);
            int length = bytes.Length;
            fixed (byte* p = bytes)
            {
                byte* pSource = p;
                Table.ShaderSource((uint)shader, 1, &pSource, &length);
            }
        }

        public static void CompileShader(int shader) => Table.CompileShader((uint)shader);

        public static void GetShader(int shader, ShaderParameter pname, out int param)
        {
            int value = 0;
            Table.GetShaderiv((uint)shader, (uint)pname, &value);
            param = value;
        }

        public static string GetShaderInfoLog(int shader)
        {
            int length = 0;
            Table.GetShaderiv((uint)shader, (uint)ShaderParameter.InfoLogLength, &length);

            if (length <= 0)
                return string.Empty;

            byte[] buf = new byte[length];
            fixed (byte* p = buf)
            {
                int actual;
                Table.GetShaderInfoLog((uint)shader, length, &actual, p);
                return Encoding.UTF8.GetString(buf, 0, actual);
            }
        }

        public static void DeleteShader(int shader) => Table.DeleteShader((uint)shader);

        public static int CreateProgram() => (int)Table.CreateProgram();

        public static void AttachShader(int program, int shader) => Table.AttachShader((uint)program, (uint)shader);

        public static void DetachShader(int program, int shader) => Table.DetachShader((uint)program, (uint)shader);

        public static void LinkProgram(int program) => Table.LinkProgram((uint)program);

        public static void GetProgram(int program, GetProgramParameterName pname, out int param)
        {
            int value = 0;
            Table.GetProgramiv((uint)program, (uint)pname, &value);
            param = value;
        }

        public static string GetProgramInfoLog(int program)
        {
            int length = 0;
            Table.GetProgramiv((uint)program, 0x8B84u /* GL_INFO_LOG_LENGTH */, &length);

            if (length <= 0)
                return string.Empty;

            byte[] buf = new byte[length];
            fixed (byte* p = buf)
            {
                int actual;
                Table.GetProgramInfoLog((uint)program, length, &actual, p);
                return Encoding.UTF8.GetString(buf, 0, actual);
            }
        }

        public static void DeleteProgram(int program) => Table.DeleteProgram((uint)program);

        public static void UseProgram(int program) => Table.UseProgram((uint)program);

        public static int GetUniformLocation(int program, string name)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name + '\0');
            fixed (byte* p = bytes)
                return Table.GetUniformLocation((uint)program, p);
        }

        public static int GetUniformBlockIndex(int program, string uniformBlockName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(uniformBlockName + '\0');
            fixed (byte* p = bytes)
                return (int)Table.GetUniformBlockIndex((uint)program, p);
        }

        public static void UniformBlockBinding(int program, int uniformBlockIndex, int uniformBlockBinding)
            => Table.UniformBlockBinding((uint)program, (uint)uniformBlockIndex, (uint)uniformBlockBinding);

        public static void Uniform1(int location, int v0) => Table.Uniform1i(location, v0);
        public static void Uniform1(int location, float v0) => Table.Uniform1f(location, v0);
        public static void Uniform2(int location, float v0, float v1) => _uniform2f(location, v0, v1);
        public static void Uniform3(int location, float v0, float v1, float v2) => _uniform3f(location, v0, v1, v2);
        public static void Uniform4(int location, float v0, float v1, float v2, float v3) => _uniform4f(location, v0, v1, v2, v3);

        public static void UniformMatrix3(int location, int count, bool transpose, ref float value)
        {
            fixed (float* p = &value)
                Table.UniformMatrix3fv(location, count, transpose ? (byte)1 : (byte)0, p);
        }

        public static void UniformMatrix4(int location, int count, bool transpose, ref float value)
        {
            fixed (float* p = &value)
                Table.UniformMatrix4fv(location, count, transpose ? (byte)1 : (byte)0, p);
        }
    }

    internal static unsafe class GL4
    {
        public enum BufferRangeTarget : uint
        {
            UniformBuffer = 0x8A11,
            ShaderStorageBuffer = 0x90D2,
        }

        internal static class GL
        {
            public static void ClearDepth(double depth)
            {
                if (OpenGL.GL.Table.ClearDepth != null)
                    OpenGL.GL.Table.ClearDepth(depth);
                else
                    OpenGL.GL.Table.ClearDepthF((float)depth);
            }

            public static void ShaderStorageBlockBinding(int program, int storageBlockIndex, int storageBlockBinding)
            {
                if (OpenGL.GL.Table.ShaderStorageBlockBinding != null)
                    OpenGL.GL.Table.ShaderStorageBlockBinding((uint)program, (uint)storageBlockIndex, (uint)storageBlockBinding);
            }

            public static void BindBufferBase(BufferRangeTarget target, int index, int buffer)
                => OpenGL.GL.Table.BindBufferBase((uint)target, (uint)index, (uint)buffer);

            public static uint GetProgramResourceIndex(int program, ProgramInterface iface, string name)
            {
                if (OpenGL.GL.Table.GetProgramResourceIndex == null)
                    return uint.MaxValue;

                byte[] bytes = Encoding.UTF8.GetBytes(name + '\0');
                fixed (byte* p = bytes)
                    return OpenGL.GL.Table.GetProgramResourceIndex((uint)program, (uint)iface, p);
            }
        }
    }
}
