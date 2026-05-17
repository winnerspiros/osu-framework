// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using SharpGen.Runtime;
using Veldrid;
using Veldrid.MetalBindings;
using Veldrid.OpenGLBindings;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Vulkan;
using GraphicsBackend = Veldrid.GraphicsBackend;
using PixelFormat = Veldrid.PixelFormat;
using PrimitiveTopology = Veldrid.PrimitiveTopology;
using StencilOperation = Veldrid.StencilOperation;
using VertexAttribPointerType = osu.Framework.Graphics.Rendering.Vertices.VertexAttribPointerType;

namespace osu.Framework.Graphics.Veldrid
{
    internal static class VeldridExtensions
    {
        public static RgbaFloat ToRgbaFloat(this Colour4 colour) => new RgbaFloat(colour.R, colour.G, colour.B, colour.A);

        public static BlendFactor ToBlendFactor(this BlendingType type) => type switch
        {
            BlendingType.DstAlpha => BlendFactor.DestinationAlpha,
            BlendingType.DstColor => BlendFactor.DestinationColor,
            BlendingType.SrcAlpha => BlendFactor.SourceAlpha,
            BlendingType.SrcColor => BlendFactor.SourceColor,
            BlendingType.OneMinusDstAlpha => BlendFactor.InverseDestinationAlpha,
            BlendingType.OneMinusDstColor => BlendFactor.InverseDestinationColor,
            BlendingType.OneMinusSrcAlpha => BlendFactor.InverseSourceAlpha,
            BlendingType.OneMinusSrcColor => BlendFactor.InverseSourceColor,
            BlendingType.One => BlendFactor.One,
            BlendingType.Zero => BlendFactor.Zero,
            BlendingType.ConstantColor => BlendFactor.BlendFactor,
            BlendingType.OneMinusConstantColor => BlendFactor.InverseBlendFactor,
            // todo: veldrid has no support for those, we may want to consider removing them from BlendingType enum (we don't even provide a blend factor in the parameters).
            _ => default,
        };

        public static BlendFunction ToBlendFunction(this BlendingEquation equation) => equation switch
        {
            BlendingEquation.Add => BlendFunction.Add,
            BlendingEquation.Subtract => BlendFunction.Subtract,
            BlendingEquation.ReverseSubtract => BlendFunction.ReverseSubtract,
            BlendingEquation.Min => BlendFunction.Minimum,
            BlendingEquation.Max => BlendFunction.Maximum,
            _ => default,
        };

        public static ColorWriteMask ToColorWriteMask(this BlendingMask mask)
        {
            ColorWriteMask writeMask = ColorWriteMask.None;

            if (mask.HasFlagFast(BlendingMask.Red)) writeMask |= ColorWriteMask.Red;
            if (mask.HasFlagFast(BlendingMask.Green)) writeMask |= ColorWriteMask.Green;
            if (mask.HasFlagFast(BlendingMask.Blue)) writeMask |= ColorWriteMask.Blue;
            if (mask.HasFlagFast(BlendingMask.Alpha)) writeMask |= ColorWriteMask.Alpha;

            return writeMask;
        }

        public static PixelFormat[] ToPixelFormats(this RenderBufferFormat[] renderBufferFormats)
        {
            var pixelFormats = new PixelFormat[renderBufferFormats.Length];

            for (int i = 0; i < pixelFormats.Length; i++)
            {
                pixelFormats[i] = renderBufferFormats[i] switch
                {
                    RenderBufferFormat.D16 => PixelFormat.R16UNorm,
                    RenderBufferFormat.D32 => PixelFormat.R32Float,
                    RenderBufferFormat.D24S8 => PixelFormat.D24UNormS8UInt,
                    RenderBufferFormat.D32S8 => PixelFormat.D32FloatS8UInt,
                    _ => throw new ArgumentException($"Unsupported render buffer format: {renderBufferFormats[i]}", nameof(renderBufferFormats)),
                };
            }

            return pixelFormats;
        }

        public static SamplerFilter ToSamplerFilter(this TextureFilteringMode mode) => mode switch
        {
            TextureFilteringMode.Linear => SamplerFilter.MinLinearMagLinearMipLinear,
            TextureFilteringMode.Nearest => SamplerFilter.MinPointMagPointMipPoint,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        public static ComparisonKind ToComparisonKind(this BufferTestFunction function) => function switch
        {
            BufferTestFunction.Always => ComparisonKind.Always,
            BufferTestFunction.Never => ComparisonKind.Never,
            BufferTestFunction.LessThan => ComparisonKind.Less,
            BufferTestFunction.Equal => ComparisonKind.Equal,
            BufferTestFunction.LessThanOrEqual => ComparisonKind.LessEqual,
            BufferTestFunction.GreaterThan => ComparisonKind.Greater,
            BufferTestFunction.NotEqual => ComparisonKind.NotEqual,
            BufferTestFunction.GreaterThanOrEqual => ComparisonKind.GreaterEqual,
            _ => throw new ArgumentOutOfRangeException(nameof(function)),
        };

        public static StencilOperation ToStencilOperation(this Rendering.StencilOperation operation) => operation switch
        {
            Rendering.StencilOperation.Zero => StencilOperation.Zero,
            Rendering.StencilOperation.Invert => StencilOperation.Invert,
            Rendering.StencilOperation.Keep => StencilOperation.Keep,
            Rendering.StencilOperation.Replace => StencilOperation.Replace,
            Rendering.StencilOperation.Increase => StencilOperation.IncrementAndClamp,
            Rendering.StencilOperation.Decrease => StencilOperation.DecrementAndClamp,
            Rendering.StencilOperation.IncreaseWrap => StencilOperation.IncrementAndWrap,
            Rendering.StencilOperation.DecreaseWrap => StencilOperation.DecrementAndWrap,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        public static VertexElementFormat ToVertexElementFormat(this VertexAttribPointerType type, int count) => (type, count) switch
        {
            (VertexAttribPointerType.Byte, 2) => VertexElementFormat.SByte2,
            (VertexAttribPointerType.Byte, 4) => VertexElementFormat.SByte4,
            (VertexAttribPointerType.UnsignedByte, 2) => VertexElementFormat.Byte2,
            (VertexAttribPointerType.UnsignedByte, 4) => VertexElementFormat.Byte4,
            (VertexAttribPointerType.Short, 2) => VertexElementFormat.Short2,
            (VertexAttribPointerType.Short, 4) => VertexElementFormat.Short4,
            (VertexAttribPointerType.UnsignedShort, 2) => VertexElementFormat.UShort2,
            (VertexAttribPointerType.UnsignedShort, 4) => VertexElementFormat.UShort4,
            (VertexAttribPointerType.Int, 1) => VertexElementFormat.Int1,
            (VertexAttribPointerType.Int, 2) => VertexElementFormat.Int2,
            (VertexAttribPointerType.Int, 3) => VertexElementFormat.Int3,
            (VertexAttribPointerType.Int, 4) => VertexElementFormat.Int4,
            (VertexAttribPointerType.UnsignedInt, 1) => VertexElementFormat.UInt1,
            (VertexAttribPointerType.UnsignedInt, 2) => VertexElementFormat.UInt2,
            (VertexAttribPointerType.UnsignedInt, 3) => VertexElementFormat.UInt3,
            (VertexAttribPointerType.UnsignedInt, 4) => VertexElementFormat.UInt4,
            (VertexAttribPointerType.Float, 1) => VertexElementFormat.Float1,
            (VertexAttribPointerType.Float, 2) => VertexElementFormat.Float2,
            (VertexAttribPointerType.Float, 3) => VertexElementFormat.Float3,
            (VertexAttribPointerType.Float, 4) => VertexElementFormat.Float4,
            (VertexAttribPointerType.HalfFloat, 1) => VertexElementFormat.Half1,
            (VertexAttribPointerType.HalfFloat, 2) => VertexElementFormat.Half2,
            (VertexAttribPointerType.HalfFloat, 4) => VertexElementFormat.Half4,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        public static PrimitiveTopology ToPrimitiveTopology(this Rendering.PrimitiveTopology type) => type switch
        {
            Rendering.PrimitiveTopology.Points => PrimitiveTopology.PointList,
            Rendering.PrimitiveTopology.Lines => PrimitiveTopology.LineList,
            Rendering.PrimitiveTopology.LineStrip => PrimitiveTopology.LineStrip,
            Rendering.PrimitiveTopology.Triangles => PrimitiveTopology.TriangleList,
            Rendering.PrimitiveTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        public static GraphicsPipelineDescription Clone(this GraphicsPipelineDescription pipeline)
        {
            pipeline.BlendState.AttachmentStates = (BlendAttachmentDescription[])pipeline.BlendState.AttachmentStates.Clone();
            pipeline.ShaderSet.Shaders = (Shader[])pipeline.ShaderSet.Shaders.Clone();
            pipeline.ShaderSet.VertexLayouts = (VertexLayoutDescription[])pipeline.ShaderSet.VertexLayouts.Clone();

            for (int i = 0; i < pipeline.ShaderSet.VertexLayouts.Length; i++)
                pipeline.ShaderSet.VertexLayouts[i].Elements = (VertexElementDescription[])pipeline.ShaderSet.VertexLayouts[i].Elements.Clone();

            pipeline.ShaderSet.Specializations = (SpecializationConstant[]?)pipeline.ShaderSet.Specializations?.Clone();
            if (pipeline.ResourceLayouts != null)
                pipeline.ResourceLayouts = (ResourceLayout[])pipeline.ResourceLayouts.Clone();
            pipeline.Outputs.ColorAttachments = (OutputAttachmentDescription[])pipeline.Outputs.ColorAttachments.Clone();

            return pipeline;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void LogD3D11(this GraphicsDevice device, out int maxTextureSize)
        {
            Debug.Assert(device.BackendType == GraphicsBackend.Direct3D11);

            var info = device.GetD3D11Info();

            // Read FeatureLevel directly from the winnerspiros/veldrid fork's BackendInfoD3D11 (cached on the device wrapper)
            // instead of materializing an extra ID3D11Device COM RCW from the IntPtr just to read one property.
            string featureLevel = info.FeatureLevel.ToString().Replace("Level_", string.Empty).Replace("_", ".");

            var dxgiAdapter = MarshallingHelpers.FromPointer<IDXGIAdapter>(info.Adapter).AsNonNull();
            var adapterDesc = dxgiAdapter.Description;

            maxTextureSize = ID3D11Resource.MaximumTexture2DSize;

            Logger.Log($@"Direct3D 11 Initialized
                        Direct3D 11 Feature Level:           {featureLevel}
                        Direct3D 11 Adapter:                 {adapterDesc.Description}
                        Direct3D 11 Adapter PCI ID:          0x{info.DeviceId:X8}
                        Direct3D 11 Dedicated Video Memory:  {adapterDesc.DedicatedVideoMemory / 1024 / 1024} MB
                        Direct3D 11 Dedicated System Memory: {adapterDesc.DedicatedSystemMemory / 1024 / 1024} MB
                        Direct3D 11 Shared System Memory:    {adapterDesc.SharedSystemMemory / 1024 / 1024} MB");
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void LogD3D12(this GraphicsDevice device, out int maxTextureSize)
        {
            Debug.Assert(device.BackendType == GraphicsBackend.Direct3D12);

            var info = device.GetD3D12Info();

            // D3D12 uses the same DXGI factory; query the adapter via the factory pointer.
            var dxgiFactory = MarshallingHelpers.FromPointer<IDXGIFactory4>(info.DxgiFactory).AsNonNull();

            string adapterDescription = "Unknown";
            long dedicatedVideoMemory = 0;
            long dedicatedSystemMemory = 0;
            long sharedSystemMemory = 0;

            if (dxgiFactory.EnumAdapters(0, out IDXGIAdapter? adapter).Success && adapter != null)
            {
                var desc = adapter.Description;
                adapterDescription = desc.Description;
                dedicatedVideoMemory = desc.DedicatedVideoMemory / 1024 / 1024;
                dedicatedSystemMemory = desc.DedicatedSystemMemory / 1024 / 1024;
                sharedSystemMemory = desc.SharedSystemMemory / 1024 / 1024;
            }

            // D3D12 max texture size is 16384 (D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION)
            maxTextureSize = 16384;

            bool supportsEnhancedBarriers = info.SupportsEnhancedBarriers;
            bool supportsMeshShaders = info.SupportsMeshShaders;
            bool supportsVrs = info.SupportsVariableRateShading;
            bool supportsRaytracing = info.SupportsRaytracing;

            Logger.Log($@"Direct3D 12 Initialized
                        Direct3D 12 Adapter:                 {adapterDescription}
                        Direct3D 12 Dedicated Video Memory:  {dedicatedVideoMemory} MB
                        Direct3D 12 Dedicated System Memory: {dedicatedSystemMemory} MB
                        Direct3D 12 Shared System Memory:    {sharedSystemMemory} MB
                        Direct3D 12 Enhanced Barriers:       {supportsEnhancedBarriers}
                        Direct3D 12 Mesh Shaders:            {supportsMeshShaders}
                        Direct3D 12 Variable Rate Shading:   {supportsVrs}
                        Direct3D 12 Raytracing:              {supportsRaytracing}");
        }

        public static unsafe void LogOpenGL(this GraphicsDevice device, out int maxTextureSize)
        {
            var info = device.GetOpenGLInfo();

            // Version and ShadingLanguageVersion are cached on BackendInfoOpenGL by the winnerspiros/veldrid fork
            // (snapshot taken at device construction). Read them off-thread to avoid two extra unsafe glGetString +
            // Marshal.PtrToStringUTF8 round-trips inside the GL execution scope below — the GL thread only needs to
            // service Renderer/Vendor (not exposed by the fork API) and the MaxTextureSize integer query.
            string version = info.Version;
            string glslVersion = info.ShadingLanguageVersion;
            string extensions = string.Join(' ', info.Extensions);

            string renderer = string.Empty;
            string vendor = string.Empty;
            int glMaxTextureSize = 0;

            info.ExecuteOnGLThread(() =>
            {
                renderer = Marshal.PtrToStringUTF8((IntPtr)OpenGLNative.glGetString(StringName.Renderer)) ?? string.Empty;
                vendor = Marshal.PtrToStringUTF8((IntPtr)OpenGLNative.glGetString(StringName.Vendor)) ?? string.Empty;

                int size;
                OpenGLNative.glGetIntegerv(GetPName.MaxTextureSize, &size);
                glMaxTextureSize = size;
            });

            maxTextureSize = glMaxTextureSize;

            Logger.Log($@"GL Initialized
                                    GL Version:                 {version}
                                    GL Renderer:                {renderer}
                                    GL Shader Language version: {glslVersion}
                                    GL Vendor:                  {vendor}
                                    GL Extensions:              {extensions}");
        }

        public static unsafe void LogVulkan(this GraphicsDevice device, out int maxTextureSize)
        {
            Debug.Assert(device.BackendType == GraphicsBackend.Vulkan);

            var info = device.GetVulkanInfo();
            IntPtr physicalDevice = info.PhysicalDevice;

            // Use BackendInfoVulkan (winnerspiros/veldrid fork) for cached extension lists and capability flags
            // instead of re-issuing native vkEnumerate*ExtensionProperties calls and unsafe marshalling.
            var instanceExtensionNames = info.AvailableInstanceExtensions;
            var deviceExtensionNames = info.AvailableDeviceExtensions;

            var vkInstance = new VkInstance(info.Instance);
            var instanceApi = new VkInstanceApi(in vkInstance);
            VkPhysicalDeviceProperties properties = instanceApi.vkGetPhysicalDeviceProperties(new VkPhysicalDevice(physicalDevice));

            maxTextureSize = (int)properties.limits.maxImageDimension2D;

            string vulkanName = RuntimeInfo.IsApple ? "MoltenVK" : "Vulkan";

            var extensionNames = new List<string>(instanceExtensionNames.Count + deviceExtensionNames.Count);
            extensionNames.AddRange(instanceExtensionNames);
            for (int i = 0; i < deviceExtensionNames.Count; i++)
                extensionNames.Add(deviceExtensionNames[i].Name);

            uint apiMajor = properties.apiVersion.Major;
            uint apiMinor = properties.apiVersion.Minor;
            uint apiPatch = properties.apiVersion.Patch;
            string apiVersion = $"{apiMajor}.{apiMinor}.{apiPatch}";
            string driverVersion;

            // https://github.com/SaschaWillems/vulkan.gpuinfo.org/blob/1e6ca6e3c0763daabd6a101b860ab4354a07f5d3/functions.php#L293-L325
            if (properties.vendorID == 0x10DE) // NVIDIA's versioning convention
                driverVersion = $"{properties.driverVersion >> 22}.{(properties.driverVersion >> 14) & 0x0FFU}.{(properties.driverVersion >> 6) & 0x0FFU}.{properties.driverVersion & 0x003U}";
            else if (properties.vendorID == 0x8086 && RuntimeInfo.OS == RuntimeInfo.Platform.Windows) // Intel's versioning convention on Windows
                driverVersion = $"{properties.driverVersion >> 22}.{properties.driverVersion & 0x3FFFU}";
            else // Vulkan's convention
                driverVersion = $"{properties.driverVersion >> 22}.{(properties.driverVersion >> 12) & 0x3FFU}.{properties.driverVersion & 0xFFFU}";

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Android && (apiMajor < 1 || (apiMajor == 1 && apiMinor < 3)))
                Logger.Log($"Vulkan {apiVersion} detected on Android. Vulkan 1.3+ is recommended for optimal performance.", level: LogLevel.Important);

            // Surface fork-only capability flags & driver identifiers for diagnostics / bug reports.
            string driverName = info.DriverName ?? "(unknown)";
            string driverInfo = info.DriverInfo ?? "(unknown)";

            string deviceName = Marshal.PtrToStringUTF8((nint)(&properties.deviceName)) ?? string.Empty;

            Logger.Log($@"{vulkanName} Initialized
                                    {vulkanName} API Version:                      {apiVersion}
                                    {vulkanName} Driver Version:                   {driverVersion}
                                    {vulkanName} Driver Name:                      {driverName}
                                    {vulkanName} Driver Info:                      {driverInfo}
                                    {vulkanName} Device:                           {deviceName}
                                    {vulkanName} Fragment Shading Rate:            {info.HasFragmentShadingRate}
                                    {vulkanName} Mesh Shader:                      {info.HasMeshShader}
                                    {vulkanName} Synchronization2:                 {info.HasSynchronization2}
                                    {vulkanName} Timeline Semaphore:               {info.HasTimelineSemaphore}
                                    {vulkanName} Display Timing (GOOGLE):          {info.HasDisplayTiming}
                                    {vulkanName} Pipeline Creation Cache Control:  {info.HasPipelineCreationCacheControl}
                                    {vulkanName} Extensions:                       {string.Join(',', extensionNames)}");
        }

        public static void LogMetal(this GraphicsDevice device, out int maxTextureSize)
        {
            Debug.Assert(device.BackendType == GraphicsBackend.Metal);

            var info = device.GetMetalInfo();
            var maxFeatureSet = info.MaxFeatureSet;

            string[] featureSetParts = maxFeatureSet.ToString().Split('_');
            string featureDevice = featureSetParts[0];
            string featureFamily = featureSetParts[1].Replace("GPUFamily", string.Empty);
            string featureVersion = featureSetParts[2];

            // https://developer.apple.com/metal/Metal-Feature-Set-Tables.pdf
            if (maxFeatureSet <= MTLFeatureSet.iOS_GPUFamily4_v1)
                maxTextureSize = maxFeatureSet <= MTLFeatureSet.iOS_GPUFamily1_v4 ? 8192 : 16384;
            else if (maxFeatureSet <= MTLFeatureSet.tvOS_GPUFamily2_v1)
                maxTextureSize = maxFeatureSet <= MTLFeatureSet.tvOS_GPUFamily1_v3 ? 8192 : 16384;
            else
                maxTextureSize = 16384;

            // Fork's BackendInfoMetal exposes the full set of supported MTLFeatureSets (not just the max),
            // surfaced here for diagnostics / bug reports.
            Logger.Log($@"Metal Initialized
                        Metal Feature Set:          {featureDevice} GPU family {featureFamily} ({featureVersion})
                        Metal Supported Feature Sets: {info.FeatureSet.Count}");
        }
    }
}
