// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Veldrid.Buffers;
using osu.Framework.Graphics.Veldrid.Shaders;
using osu.Framework.Graphics.Veldrid.Textures;
using osu.Framework.Statistics;
using Veldrid;

namespace osu.Framework.Graphics.Veldrid.Pipelines
{
    /// <summary>
    /// A pipeline that facilitates drawing.
    /// </summary>
    internal class GraphicsPipeline : BasicPipeline
    {
        private static readonly GlobalStatistic<int> stat_graphics_pipeline_created = GlobalStatistics.Get<int>(nameof(VeldridRenderer), "Total pipelines created");

        private readonly Dictionary<GraphicsPipelineDescription, Pipeline> pipelineCache = new Dictionary<GraphicsPipelineDescription, Pipeline>();

        // Fixed-size array indexed by texture unit (max 16 units, matching Renderer.lastBoundTexture).
        // Avoids per-draw Dictionary hashing for what is typically 1–4 active texture units.
        private const int max_texture_units = 16;
        private readonly VeldridTextureResources?[] attachedTextures = new VeldridTextureResources?[max_texture_units];
        private int maxAttachedTextureUnit = -1; // highest occupied slot, keeps iteration O(used) not O(16)

        // UBO name → (buffer, offset-in-bytes). Merging offset into the same dict value removes
        // the separate uniformBufferOffsets dictionary and its per-draw lookup.
        private readonly Dictionary<string, (IVeldridUniformBuffer Buffer, uint Offset)> attachedUniformBuffers
            = new Dictionary<string, (IVeldridUniformBuffer, uint)>();

        // Scratch lists reused each draw call to avoid repeated layout dictionary lookups.
        private readonly List<(uint Set, VeldridTextureResources Resource, ResourceLayout Layout)> pendingTextureBindings = new List<(uint, VeldridTextureResources, ResourceLayout)>();
        private readonly List<(uint Set, IVeldridUniformBuffer Buffer, ResourceLayout Layout, uint Offset)> pendingUniformBindings = new List<(uint, IVeldridUniformBuffer, ResourceLayout, uint)>();

        private GraphicsPipelineDescription pipelineDesc = new GraphicsPipelineDescription
        {
            RasterizerState = RasterizerStateDescription.CULL_NONE,
            BlendState = BlendStateDescription.SINGLE_OVERRIDE_BLEND,
            ShaderSet = { VertexLayouts = new VertexLayoutDescription[1] }
        };

        private IVeldridFrameBuffer? currentFrameBuffer;
        private VeldridShader? currentShader;
        private VeldridIndexBuffer? currentIndexBuffer;
        private DeviceBuffer? currentVertexBuffer;
        private VertexLayoutDescription currentVertexLayout;

        // Cache the last successfully activated pipeline so that draw calls with identical state
        // can skip the dictionary hash-and-lookup in createPipeline(). Invalidated whenever any
        // field of pipelineDesc changes.
        private Pipeline? cachedPipeline;
        private bool pipelineDescDirty = true;

        public GraphicsPipeline(VeldridDevice device)
            : base(device)
        {
            pipelineDesc.Outputs = Device.SwapchainFramebuffer.OutputDescription;
        }

        public override void Begin()
        {
            base.Begin();

            Array.Clear(attachedTextures);
            maxAttachedTextureUnit = -1;
            attachedUniformBuffers.Clear();
            currentFrameBuffer = null;
            currentShader = null;
            currentIndexBuffer = null;
            currentVertexBuffer = null;
            cachedPipeline = null;
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Clears the currently bound frame buffer.
        /// </summary>
        /// <param name="clearInfo">The clearing parameters.</param>
        public void Clear(ClearInfo clearInfo)
        {
            Commands.ClearColorTarget(0, clearInfo.Colour.ToRgbaFloat());

            var framebuffer = currentFrameBuffer?.Framebuffer ?? Device.SwapchainFramebuffer;
            if (framebuffer.DepthTarget != null)
                Commands.ClearDepthStencil((float)clearInfo.Depth, (byte)clearInfo.Stencil);
        }

        /// <summary>
        /// Sets the active scissor state.
        /// </summary>
        /// <param name="enabled">Whether the scissor test is enabled.</param>
        public void SetScissorState(bool enabled)
        {
            pipelineDesc.RasterizerState.ScissorTestEnabled = enabled;
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Sets the active shader.
        /// </summary>
        /// <param name="shader">The shader.</param>
        public void SetShader(VeldridShader shader)
        {
            shader.EnsureShaderInitialised();

            currentShader = shader;
            pipelineDesc.ShaderSet.Shaders = shader.Shaders;
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Sets the active blending state.
        /// </summary>
        /// <param name="blendingParameters">The blending parameters.</param>
        public void SetBlend(BlendingParameters blendingParameters)
        {
            pipelineDesc.BlendState.AttachmentStates[0].BlendEnabled = !blendingParameters.IsDisabled;
            pipelineDesc.BlendState.AttachmentStates[0].SourceColorFactor = blendingParameters.Source.ToBlendFactor();
            pipelineDesc.BlendState.AttachmentStates[0].SourceAlphaFactor = blendingParameters.SourceAlpha.ToBlendFactor();
            pipelineDesc.BlendState.AttachmentStates[0].DestinationColorFactor = blendingParameters.Destination.ToBlendFactor();
            pipelineDesc.BlendState.AttachmentStates[0].DestinationAlphaFactor = blendingParameters.DestinationAlpha.ToBlendFactor();
            pipelineDesc.BlendState.AttachmentStates[0].ColorFunction = blendingParameters.RGBEquation.ToBlendFunction();
            pipelineDesc.BlendState.AttachmentStates[0].AlphaFunction = blendingParameters.AlphaEquation.ToBlendFunction();
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Sets a mask deciding which colour components are affected during blending.
        /// </summary>
        /// <param name="blendingMask">The blending mask.</param>
        public void SetBlendMask(BlendingMask blendingMask)
        {
            pipelineDesc.BlendState.AttachmentStates[0].ColorWriteMask = blendingMask.ToColorWriteMask();
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Sets the active viewport rectangle.
        /// </summary>
        /// <param name="viewport">The viewport rectangle.</param>
        public void SetViewport(RectangleI viewport)
            => Commands.SetViewport(0, new Viewport(viewport.Left, viewport.Top, viewport.Width, viewport.Height, 0, 1));

        /// <summary>
        /// Sets the active scissor rectangle.
        /// </summary>
        /// <param name="scissor">The scissor rectangle.</param>
        public void SetScissor(RectangleI scissor)
            => Commands.SetScissorRect(0, (uint)scissor.X, (uint)scissor.Y, (uint)scissor.Width, (uint)scissor.Height);

        /// <summary>
        /// Sets the active depth parameters.
        /// </summary>
        /// <param name="depthInfo">The depth parameters.</param>
        public void SetDepthInfo(DepthInfo depthInfo)
        {
            pipelineDesc.DepthStencilState.DepthTestEnabled = depthInfo.DepthTest;
            pipelineDesc.DepthStencilState.DepthWriteEnabled = depthInfo.WriteDepth;
            pipelineDesc.DepthStencilState.DepthComparison = depthInfo.Function.ToComparisonKind();
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Sets the active stencil parameters.
        /// </summary>
        /// <param name="stencilInfo">The stencil parameters.</param>
        public void SetStencilInfo(StencilInfo stencilInfo)
        {
            pipelineDesc.DepthStencilState.StencilTestEnabled = stencilInfo.StencilTest;
            pipelineDesc.DepthStencilState.StencilReference = (uint)stencilInfo.TestValue;
            pipelineDesc.DepthStencilState.StencilReadMask = pipelineDesc.DepthStencilState.StencilWriteMask = (byte)stencilInfo.Mask;
            pipelineDesc.DepthStencilState.StencilBack.Pass = pipelineDesc.DepthStencilState.StencilFront.Pass = stencilInfo.TestPassedOperation.ToStencilOperation();
            pipelineDesc.DepthStencilState.StencilBack.Fail = pipelineDesc.DepthStencilState.StencilFront.Fail = stencilInfo.StencilTestFailOperation.ToStencilOperation();
            pipelineDesc.DepthStencilState.StencilBack.DepthFail = pipelineDesc.DepthStencilState.StencilFront.DepthFail = stencilInfo.DepthTestFailOperation.ToStencilOperation();
            pipelineDesc.DepthStencilState.StencilBack.Comparison = pipelineDesc.DepthStencilState.StencilFront.Comparison = stencilInfo.TestFunction.ToComparisonKind();
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Sets the active framebuffer.
        /// </summary>
        /// <param name="frameBuffer">The framebuffer, or <c>null</c> to activate the back-buffer.</param>
        public void SetFrameBuffer(IVeldridFrameBuffer? frameBuffer)
        {
            currentFrameBuffer = frameBuffer;

            Framebuffer fb = frameBuffer?.Framebuffer ?? Device.SwapchainFramebuffer;

            Commands.SetFramebuffer(fb);
            pipelineDesc.Outputs = fb.OutputDescription;
            pipelineDescDirty = true;
        }

        /// <summary>
        /// Sets the active vertex buffer.
        /// </summary>
        /// <param name="buffer">The vertex buffer.</param>
        /// <param name="layout">The layout of vertices in the buffer.</param>
        public void SetVertexBuffer(DeviceBuffer buffer, VertexLayoutDescription layout)
        {
            if (buffer == currentVertexBuffer && layout.Equals(currentVertexLayout))
                return;

            Commands.SetVertexBuffer(0, buffer);
            pipelineDesc.ShaderSet.VertexLayouts[0] = layout;
            pipelineDescDirty = true;

            FrameStatistics.Increment(StatisticsCounterType.VBufBinds);

            currentVertexBuffer = buffer;
            currentVertexLayout = layout;
        }

        /// <summary>
        /// Sets the active index buffer.
        /// </summary>
        /// <param name="indexBuffer">The index buffer.</param>
        public void SetIndexBuffer(VeldridIndexBuffer indexBuffer)
        {
            if (currentIndexBuffer == indexBuffer)
                return;

            currentIndexBuffer = indexBuffer;
            Commands.SetIndexBuffer(indexBuffer.Buffer, VeldridIndexBuffer.FORMAT);
        }

        /// <summary>
        /// Attaches a texture to the pipeline at the given texture unit.
        /// </summary>
        /// <param name="unit">The texture unit.</param>
        /// <param name="texture">The texture.</param>
        public void AttachTexture(int unit, IVeldridTexture texture)
        {
            var resources = texture.GetResourceList();

            for (int i = 0; i < resources.Count; i++)
            {
                attachedTextures[unit] = resources[i];

                if (unit > maxAttachedTextureUnit)
                    maxAttachedTextureUnit = unit;

                unit++;
            }
        }

        /// <summary>
        /// Attaches a uniform buffer to the pipeline at the given uniform block.
        /// </summary>
        /// <param name="name">The uniform block name.</param>
        /// <param name="buffer">The uniform buffer.</param>
        public void AttachUniformBuffer(string name, IVeldridUniformBuffer buffer)
            => attachedUniformBuffers[name] = (buffer, 0);

        /// <summary>
        /// Sets the offset of a uniform buffer that was previously attached via <see cref="AttachUniformBuffer"/>.
        /// </summary>
        /// <param name="buffer">The uniform buffer whose offset should be updated.</param>
        /// <param name="bufferOffsetInBytes">The new offset in bytes.</param>
        public void SetUniformBufferOffset(IVeldridUniformBuffer buffer, uint bufferOffsetInBytes)
        {
            // Linear scan is intentional: there are typically 2–4 uniform buffers per shader,
            // so this is faster than an additional Dictionary<IVeldridUniformBuffer, string> lookup.
            foreach (var (name, entry) in attachedUniformBuffers)
            {
                if (ReferenceEquals(entry.Buffer, buffer))
                {
                    attachedUniformBuffers[name] = (buffer, bufferOffsetInBytes);
                    return;
                }
            }
        }

        /// <summary>
        /// Draws vertices from the active vertex buffer.
        /// </summary>
        /// <param name="topology">The vertex topology.</param>
        /// <param name="vertexStart">The vertex at which to start drawing.</param>
        /// <param name="verticesCount">The number of vertices to draw.</param>
        /// <param name="vertexIndexOffset">The base vertex value at which to start reading from.</param>
        /// <remarks>
        /// The choice of value for <paramref name="vertexStart"/> and <paramref name="vertexIndexOffset"/> depends on the specific use-case:
        /// <list type="bullet">
        ///   <item><paramref name="vertexStart"/> offsets where in the index buffer to start reading from.</item>
        ///   <item><paramref name="vertexIndexOffset"/> offsets where in the vertex buffer to start reading from.</item>
        /// </list>
        /// </remarks>
        /// <exception cref="InvalidOperationException">If no shader or index buffer is active.</exception>
        public void DrawVertices(global::Veldrid.PrimitiveTopology topology, int vertexStart, int verticesCount, int vertexIndexOffset = 0)
        {
            if (currentShader == null)
                throw new InvalidOperationException("No shader bound.");

            if (currentIndexBuffer == null)
                throw new InvalidOperationException("No index buffer bound.");

            if (pipelineDesc.PrimitiveTopology != topology)
            {
                pipelineDesc.PrimitiveTopology = topology;
                pipelineDescDirty = true;
            }

            // Only resize the resource layouts array when the shader's layout count actually changed.
            if (pipelineDesc.ResourceLayouts?.Length != currentShader.LayoutCount)
            {
                Array.Resize(ref pipelineDesc.ResourceLayouts, currentShader.LayoutCount);
                pipelineDescDirty = true;
            }

            // Phase 1: look up layouts once per resource, populate pipelineDesc, and cache results
            // in scratch lists so the binding phase below needs no additional dictionary lookups.
            pendingTextureBindings.Clear();

            // Iterate only up to the highest occupied unit — avoids touching all 16 slots when
            // only 1–4 are actually bound (which is the common case).
            for (int unit = 0; unit <= maxAttachedTextureUnit; unit++)
            {
                var resource = attachedTextures[unit];

                if (resource == null)
                    continue;

                var layout = currentShader.GetTextureLayout(unit);

                if (layout == null)
                    continue;

                if (pipelineDesc.ResourceLayouts![layout.Set] != layout.Layout)
                {
                    pipelineDesc.ResourceLayouts[layout.Set] = layout.Layout;
                    pipelineDescDirty = true;
                }

                pendingTextureBindings.Add(((uint)layout.Set, resource, layout.Layout));
            }

            pendingUniformBindings.Clear();

            foreach (var (name, (buffer, offset)) in attachedUniformBuffers)
            {
                var layout = currentShader.GetUniformBufferLayout(name);

                if (layout == null)
                    continue;

                if (pipelineDesc.ResourceLayouts![layout.Set] != layout.Layout)
                {
                    pipelineDesc.ResourceLayouts[layout.Set] = layout.Layout;
                    pipelineDescDirty = true;
                }

                pendingUniformBindings.Add(((uint)layout.Set, buffer, layout.Layout, offset));
            }

            // Activate the pipeline — use the cached instance when the description has not changed.
            Commands.SetPipeline(createPipeline());

            // Phase 2: bind resources using the cached (set, resource/buffer, layout) tuples —
            // no additional layout dictionary lookups required.
            foreach (var (set, resource, layout) in pendingTextureBindings)
                Commands.SetGraphicsResourceSet(set, resource.GetResourceSet(Factory, layout));

            foreach (var (set, buffer, layout, offset) in pendingUniformBindings)
            {
                uint off = offset;
                Commands.SetGraphicsResourceSet(set, buffer.GetResourceSet(layout), 1, ref off);
            }

            int indexStart = currentIndexBuffer.TranslateToIndex(vertexStart);
            int indicesCount = currentIndexBuffer.TranslateToIndex(verticesCount);
            Commands.DrawIndexed((uint)indicesCount, 1, (uint)indexStart, vertexIndexOffset, 0);
        }

        private Pipeline createPipeline()
        {
            if (!pipelineDescDirty && cachedPipeline != null)
                return cachedPipeline;

            pipelineDescDirty = false;

            if (!pipelineCache.TryGetValue(pipelineDesc, out var instance))
            {
                pipelineCache[pipelineDesc.Clone()] = instance = Factory.CreateGraphicsPipeline(ref pipelineDesc);
                stat_graphics_pipeline_created.Value++;
            }

            cachedPipeline = instance;
            return instance;
        }
    }
}
