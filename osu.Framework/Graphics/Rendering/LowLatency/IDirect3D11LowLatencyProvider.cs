// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Framework.Graphics.Rendering.LowLatency
{
    /// <summary>
    /// Low-latency provider specifically for Direct3D 11 backends (e.g. NVIDIA Reflex via D3D11).
    /// </summary>
    public interface IDirect3D11LowLatencyProvider : ILowLatencyProvider
    {
    }
}
