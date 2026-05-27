<p align="center">
  <img width="500px" src="assets/o!f Logo Large FC.svg">
</p>

# osu!framework

[![Build status](https://github.com/winnerspiros/osu-framework/actions/workflows/ci.yml/badge.svg?branch=master&event=push)](https://github.com/winnerspiros/osu-framework/actions/workflows/ci.yml)
[![GitHub release](https://img.shields.io/github/release/winnerspiros/osu-framework.svg)](https://github.com/winnerspiros/osu-framework/releases/latest)
[![dev chat](https://discordapp.com/api/guilds/188630481301012481/widget.png?style=shield)](https://discord.gg/ppy)

A high-performance, cross-platform game framework written with [osu!](https://github.com/ppy/osu) in mind.

> **This is the [winnerspiros/osu-framework](https://github.com/winnerspiros/osu-framework) performance fork.** It tracks [ppy/osu-framework](https://github.com/ppy/osu-framework) upstream and layers latency/throughput improvements on top. See [Changes from upstream](#changes-from-upstream-ppyosu-framework) for the full diff.

---

## ✨ Key Features

| Category | Highlights |
|----------|-----------|
| **Rendering** | Multi-backend via Veldrid: Direct3D 11, Direct3D 12, Vulkan, Metal, OpenGL |
| **Audio** | BASS engine with per-platform low-latency tuning (WASAPI, AAudio, CoreAudio, PipeWire) |
| **Input** | Raw keyboard, async key events, high-frequency touch, tablet, joystick |
| **Low Latency** | NVIDIA Reflex / LatencyFlex integration, WASAPI event-driven, AAudio minimal buffers |
| **Platforms** | Windows, Linux, macOS, Android (API 33+), iOS (13.4+) |
| **Runtime** | .NET 10, C# 14, System.Threading.Lock, profiled AOT on mobile |
| **Testing** | Visual test framework, per-component isolation, headless CI support |

---

## 🖥️ Platform Support

| Platform | Renderer | Audio Backend | Low-Latency Audio | Min Version |
|----------|----------|---------------|-------------------|-------------|
| **Windows** | D3D11 / D3D12 / Vulkan / OpenGL | BASS + WASAPI (shared/exclusive) | ✅ WASAPI event-driven (~3–5 ms) | Windows 10+ |
| **Linux** | Vulkan / OpenGL | BASS + PipeWire / PulseAudio / ALSA | ✅ PipeWire reduced quantum (~5 ms) | Kernel 5.4+ |
| **macOS** | Metal / OpenGL (deprecated) | BASS + CoreAudio | ✅ Reduced I/O buffer (~3–5 ms) | macOS 12+ |
| **Android** | Vulkan / OpenGL ES | BASS + AAudio | ✅ AAudio low-latency mode (~5–10 ms) | API 33 (Android 13) |
| **iOS** | Metal | BASS + CoreAudio | ✅ Reduced I/O buffer (~3–5 ms) | iOS 13.4+ |

---

## 🚀 Getting Started

### For game developers

If you want to **create a game** using the framework:
1. Start from the [getting started wiki](https://github.com/ppy/osu-framework/wiki/Setting-up-your-first-project)
2. Or use the [project templates](https://github.com/ppy/osu-framework/tree/master/osu.Framework.Templates) directly
3. Full cross-platform support, testing setup, and project structure included out of the box

### For framework contributors

The rest of this README is for working **on** the framework itself.

---

## 📋 Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) (all platforms)
- **Linux:** system-wide FFmpeg for video decoding
- **Android:** JDK 17, Android workload (`dotnet workload install android`)
- **iOS:** Xcode 26+, iOS workload (`dotnet workload install ios`)
- **IDE:** [Visual Studio 2022+](https://visualstudio.microsoft.com/vs/), [JetBrains Rider](https://www.jetbrains.com/rider/), or [VS Code](https://code.visualstudio.com/) with C# + EditorConfig extensions

### Building

```bash
# Desktop (Windows/Linux/macOS)
dotnet build -c Debug osu-framework.Desktop.slnf

# Android
dotnet workload install android
dotnet build -c Debug osu-framework.Android.slnf

# iOS (macOS only)
dotnet workload install ios
dotnet build -c Debug osu-framework.iOS.slnf
```

> **IDE users:** Load the platform-specific `.slnf` file (not the main `.sln`) for access to template run configurations.

### Code analysis

```bash
# PowerShell
./InspectCode.ps1

# Bash
./InspectCode.sh

# Code style enforcement
dotnet build -c Debug -warnaserror osu-framework.Desktop.slnf -p:EnforceCodeStyleInBuild=true
```

---

## 🎚️ Audio Latency Modes

The framework provides a configurable `AudioLatencyMode` setting that selects the optimal backend and buffer sizes for each platform:

| Mode | Windows | Linux | macOS | Android | iOS |
|------|---------|-------|-------|---------|-----|
| **Standard** | DirectSound | PulseAudio defaults | CoreAudio default | AAudio ~512 samples | CoreAudio default |
| **Low Latency** | WASAPI shared (~3–5 ms) | PipeWire ~256 samples | CoreAudio ~256 samples | AAudio ~256 samples | CoreAudio ~256 samples |
| **Minimal** | WASAPI exclusive/min period | PipeWire/JACK ~128 samples | CoreAudio ~128 samples | AAudio ~128 samples | CoreAudio ~128 samples |

Configure via `FrameworkSetting.AudioLatencyMode` in code or the `framework.ini` file.

> **Note:** The legacy `AudioUseExperimentalWasapi` setting is still supported and interoperates with the new latency mode (enabling low-latency/minimal on Windows will automatically activate WASAPI).

---

## ⚡ Low-Latency Rendering

The framework includes a generic low-latency rendering infrastructure:

- **`ILowLatencyProvider`** — interface for GPU-side latency reduction (NVIDIA Reflex, LatencyFlex)
- **Latency markers** — `SimulationStart/End`, `RenderSubmitStart/End`, `PresentStart/End`, `InputSample`
- **`FrameSleep()`** — provider-controlled sleep for Reflex Boost mode
- **`LatencyMode` setting** — `Off` / `On` / `Boost` via `FrameworkConfigManager`
- Supports both D3D11 and D3D12 native device handles

---

## �� AI Optimisation Agents

The [`agents/`](agents/) directory contains AI agent instructions for platform-specific performance analysis. Feed these to any AI tool (Copilot, Claude, ChatGPT) to get targeted optimisation suggestions:

| Agent | Focus |
|-------|-------|
| [🌐 Overall](agents/overall.md) | Cross-platform allocations, threading, data structures |
| [🟦 Windows](agents/windows.md) | WASAPI, D3D11/D3D12, Reflex, raw input |
| [🐧 Linux](agents/linux.md) | PipeWire/JACK, Vulkan, Wayland |
| [🍎 macOS](agents/macos.md) | CoreAudio, Metal, Apple Silicon |
| [🤖 Android](agents/android.md) | AAudio, Vulkan swapchain, ADPF, 16KB pages |
| [📱 iOS](agents/ios.md) | CoreAudio, Metal TBDR, AOT, thermal management |

---

## 🔄 Contributing

Contributions can be made via pull requests to this repository.

If you're unsure of what you can help with, check out the [list of open issues](https://github.com/winnerspiros/osu-framework/issues).

Before starting, please make sure you are familiar with the [development and testing](https://github.com/ppy/osu-framework/wiki/Development-and-Testing) procedure. New component development, and where possible, bug fixing and debugging **should always be done under VisualTests**.

Note that while we already have certain standards in place, nothing is set in stone. If you have an issue with the way code is structured, with any libraries we are using, or with any processes involved with contributing, *please* bring it up. We welcome all feedback so we can make contributing to this project as pain-free as possible.

---

## Migration: osuTK / OpenTK → System.Numerics + custom GL

> **This fork has fully removed the [osuTK](https://github.com/ppy/osuTK) / OpenTK dependency.** All math types, the GL binding layer, and the `Key` enum have been replaced with standard .NET / custom equivalents. There are **zero** remaining `osuTK` or `OpenTK` references in the framework source.

### Math types

| osuTK type | Replacement |
|---|---|
| `osuTK.Vector2` | `System.Numerics.Vector2` |
| `osuTK.Vector3` | `System.Numerics.Vector3` |
| `osuTK.Vector4` | `System.Numerics.Vector4` |
| `osuTK.Matrix3` | `System.Numerics.Matrix3x2` |
| `osuTK.Matrix4` | `System.Numerics.Matrix4x4` |
| `osuTK.Quaternion` | `System.Numerics.Quaternion` |
| `osuTK.Color4` | `osu.Framework.Graphics.Colour4` (unchanged) |
| `osuTK.MathHelper` | `MathF` / `float.DegreesToRadians` / `float.RadiansToDegrees` |

All osuTK-era extension methods (`Normalized()`, `PerpendicularLeft()`, `PerpendicularRight()`, `NormalizeFast()`, `PerpDot()`) are preserved as extension methods in `osu.Framework.Graphics.Vector2Extensions`. Matrix operations (`TranslateFromLeft/Right`, `RotateFromLeft/Right`, `ScaleFromLeft/Right`, `ShearFromLeft/Right`) are in `osu.Framework.Extensions.MatrixExtensions`.

**Convention unchanged:** row-vector convention (`v * M`) is used throughout, matching the System.Numerics and GLSL behaviour.

### GL binding layer

osuTK's `OpenTK.Graphics.OpenGL4.GL.*` static methods have been replaced with a custom **function-pointer table** in `osu.Framework.Graphics.OpenGL.GL`. On first use the table is populated via Veldrid's `OpenGLProcTable` (which resolves function addresses from the active GL context using `SDL_GL_GetProcAddress`). This eliminates the osuTK interop overhead and the dependency on the osuTK NuGet package entirely.

All GL enums used by the renderer (`TextureTarget`, `RenderbufferInternalFormat`, `BufferUsageHint`, etc.) are now defined directly in `osu.Framework.Graphics.OpenGL.GL` — no third-party GL bindings needed.

### Key enum

The `osu.Framework.Input.Key` enum retains its value layout (aligned to SDL scancode order) but is now entirely independent of osuTK. Because `Key` and `InputKey` (the key-binding layer enum) have **different numeric values**, `KeyCombination.FromKey()` contains a full explicit switch mapping every `Key` value to its `InputKey` counterpart — including all letter/digit/navigation/function/media keys. The fallback is `InputKey.None` (unknown key) rather than an unsafe cast.

---

## Changes from upstream [ppy/osu-framework](https://github.com/ppy/osu-framework)

This fork ([winnerspiros/osu-framework](https://github.com/winnerspiros/osu-framework)) layers the following on top of upstream. Items are grouped by area; each section lists the **what** and the **why**.

### 🔧 Build / packaging — winnerspiros forks built from source

Both Veldrid components are consumed as **`ProjectReference`s to git submodules**, not NuGet packages. The framework is always compiled against the very latest fork code.

| Submodule | URL | Notes |
|---|---|---|
| `submodules/veldrid` | [winnerspiros/veldrid](https://github.com/winnerspiros/veldrid) | net10.0 / C# 14, `System.Threading.Lock`, full **D3D12 backend**, hot-path optimisations, vtx/idx buffer caching, glInvalidateFramebuffer |
| `submodules/veldrid-spirv` | [winnerspiros/veldrid-spirv](https://github.com/winnerspiros/veldrid-spirv) | net10.0, C++17 native side, **Android 16 KB page alignment** |

**Packaging mechanics** (so the produced `ppy.osu.Framework` nupkg is fully self-contained and consumable on `nuget.org`):

- Both `ProjectReference`s use `PrivateAssets="all"`, otherwise `dotnet pack` would record phantom `ppy.Veldrid` / `ppy.Veldrid.SPIRV` dependencies pinned to NerdBank.GitVersioning-generated versions (e.g. `4.9.111-g…`) that don't exist on any feed.
- The fork-built managed DLLs (`ppy.Veldrid.dll`, `ppy.Veldrid.MetalBindings.dll`, `ppy.Veldrid.OpenGLBindings.dll`, `ppy.Veldrid.SPIRV.dll`) are bundled directly into `lib/net10.0/` of the framework nupkg via a `TargetsForTfmSpecificBuildOutput` target.
- The runtime `PackageReference`s the Veldrid fork uses (`ppy.Vk`, `Vortice.D3DCompiler`, `Vortice.Direct3D11`, `Vortice.Direct3D12`) are re-declared on `osu.Framework.csproj` so consumers still restore them.
- The pre-built C++ native binary `libveldrid-spirvcross.*` (from `ppy.Veldrid.SPIRV` NuGet, `IncludeAssets="native"`) is the **only** thing pulled from NuGet — building the C++ side from source would require CMake/clang in CI. That NuGet was itself published from `winnerspiros/veldrid-spirv@b268bf39ea`.
- `submodules/Directory.Build.targets` rewires the SPIRV submodule's stale `ppy.Veldrid 4.9.69` `PackageReference` to a sibling `ProjectReference` to the local `winnerspiros/veldrid` fork. Without this, the old upstream `ppy.Veldrid` (which lacks the `Direct3D12` enum value, `GetD3D12Info`, `CreateD3D12`) would win on the compile path and break the Windows build.
- `submodules/.editorconfig` (`root = true`) prevents osu-framework's strict style rules from being enforced on third-party fork source files.

### 🎯 Fork capabilities consumed by the framework

The fork's *backend-internal* optimisations (Vulkan pipeline cache / push descriptors / dynamic rendering / `VK_EXT_host_image_copy`, **Vulkan vtx/idx buffer caching** (skips redundant `vkCmdBind*` GPU calls), Android Vulkan swapchain pre-transform/current-extent handling, D3D12 redundant state caching, **D3D12/D3D11/Metal/Vulkan staging-pool swap-remove** (O(1) pool reclaim), **OpenGL `glInvalidateFramebuffer`** (tile-store skip for offscreen FBOs — saves tile→DRAM writeback for non-sampled attachments), **Vulkan spec compliance** (skip clear for transient textures, §19.1), **modernization sweep** (switch expressions, `System.HashCode`, `Array.Empty`, string interpolation across all backends), OpenGL pipeline state caching, Metal merged layout-offset loops, all-backend `System.Threading.Lock`, `Vortice.Windows 3.8.3`) are transparent — the framework benefits automatically with no code changes.

The framework explicitly wires the fork's new **public API surface** (`BackendInfoD3D11/D3D12/Metal/OpenGL/Vulkan`) in `VeldridExtensions.cs`:

| Backend | Fork API used | Benefit |
|---|---|---|
| **D3D11** (`LogD3D11`) | `BackendInfoD3D11.FeatureLevel`, `DeviceId` | Avoids materializing a second `ID3D11Device` COM RCW from the IntPtr just to read one property; surfaces PCI ID for bug reports |
| **D3D12** (`LogD3D12`) | `BackendInfoD3D12.SupportsEnhancedBarriers`, `SupportsMeshShaders`, `SupportsVariableRateShading`, `SupportsRaytracing` | Logs full D3D12 capability tier without re-issuing `CheckFeatureSupport` calls |
| **D3D11 + D3D12** (`ILowLatencyProvider`) | `BackendInfoD3D11.Device`, `BackendInfoD3D12.Device` | Native device handle for NVIDIA Reflex / LatencyFlex on both renderers |
| **Vulkan** (`LogVulkan`) | `BackendInfoVulkan.AvailableInstanceExtensions`, `AvailableDeviceExtensions`, `DriverName`, `DriverInfo`, `HasFragmentShadingRate`, `HasMeshShader` | No more re-issuing native `vkEnumerate*ExtensionProperties` + unsafe marshalling; surfaces fork-only capability flags |
| **OpenGL** (`LogOpenGL`) | `BackendInfoOpenGL.Version`, `ShadingLanguageVersion` (cached) | Read off-thread — saves two unsafe `glGetString` + `Marshal.PtrToStringUTF8` round-trips inside the GL execution scope; only `Renderer` / `Vendor` / `MaxTextureSize` still require the GL thread |
| **Metal** (`LogMetal`) | `BackendInfoMetal.MaxFeatureSet`, `FeatureSet` | Surfaces full supported feature-set count for diagnostics (in addition to the maximum) |

### 🟦 Direct3D 12 renderer support

- Added `RendererType.Direct3D12` / `RendererType.Deferred_Direct3D12` and `GraphicsSurfaceType.Direct3D12`.
- Full pipeline: `VeldridDevice.CreateD3D12` swapchain creation, `LogD3D12` diagnostics (adapter info, Enhanced Barriers, Mesh Shaders, VRS, Raytracing), `PersistentStagingBuffer` staging.
- D3D12 included in the Windows renderer fallback order (after D3D11, before OpenGL).
- Powered by the [winnerspiros/veldrid](https://github.com/winnerspiros/veldrid) submodule's full D3D12 backend.

### ⚡ Low-latency rendering infrastructure

- Generic `ILowLatencyProvider` interface for GPU-side latency reduction (NVIDIA Reflex, LatencyFlex, or any future API).
  - `IDirect3D11LowLatencyProvider` (D3D11-specific) extends `ILowLatencyProvider`.
  - `NoOpLowLatencyProvider` and `NoOpDirect3D11LowLatencyProvider` (default no-ops).
- Latency markers (`SimulationStart/End`, `RenderSubmitStart/End`, `PresentStart/End`, `InputSample`, `TriggerFlash`) inserted into `GameHost.UpdateFrame()` and `GameHost.DrawFrame()`.
- `FrameSleep()` called at the start of each update frame for provider-controlled sleep (Reflex Boost mode).
- Provider auto-initialises on the draw thread using the native D3D11 or D3D12 device handle from Veldrid's `BackendInfoD3D11` / `BackendInfoD3D12`.
- `LatencyMode` setting (`Off` / `On` / `Boost`) added to `FrameworkConfigManager`.
- Inspired by [upstream PR #6666](https://github.com/ppy/osu-framework/pull/6666).

### 🎚️ Audio latency modes (NEW)

- **`AudioLatencyMode`** enum (`Standard` / `LowLatency` / `Minimal`) with per-platform backend selection:
  - **Windows:** WASAPI shared/exclusive mode, configurable period
  - **Linux:** PipeWire / JACK / PulseAudio with reduced buffer quantum
  - **macOS:** CoreAudio with minimised I/O buffer duration
  - **Android:** AAudio with configurable sample count (-128 to -512)
  - **iOS:** CoreAudio with reduced I/O buffer duration
- `FrameworkSetting.AudioLatencyMode` exposed in `FrameworkConfigManager`
- Interoperates with legacy `AudioUseExperimentalWasapi` setting
- `GlobalMixerHandle` exposed publicly for external low-latency integrations (e.g. Oboe redirector)

### 🎚 Frame rate limiter enhancements

- **Unbuffered VSync (`UVSync`)**: limits both draw *and* update threads to the exact display refresh rate. Useful for VRR / G-Sync / FreeSync displays where regular VSync introduces unwanted buffering ([upstream PR #6696](https://github.com/ppy/osu-framework/pull/6696)).
- **Custom FPS limiter** (`FrameSync.Custom`): `CustomDrawLimit` 0–1000 Hz; `0` = unlimited draw thread, update thread runs at max Hz. Useful for benchmarking or VRR-specific tuning ([upstream PR #6725](https://github.com/ppy/osu-framework/pull/6725)).

### ⌨️ Input latency improvements

- **Raw keyboard input on Windows**: `SDL_HINT_WINDOWS_RAW_KEYBOARD` enabled by default — bypasses the Windows message translation layer for lower-latency key events ([upstream PR #6507](https://github.com/ppy/osu-framework/pull/6507)).
- **Async keyboard event handling**: when text input (IME) is not active, `KEY_DOWN` / `KEY_UP` are handled directly in SDL's event filter (`HandleEventFromFilter`), bypassing the SDL event queue for reduced input-to-render latency ([upstream PR #6506](https://github.com/ppy/osu-framework/pull/6506)).

### 🚀 Performance optimisations

#### Threading / synchronisation

Every `lock (someObject)` site in the framework has been migrated from the legacy `Monitor`-based pattern to `System.Threading.Lock`, which uses a purpose-built kernel primitive on .NET 9+/CLR and avoids the extra `typeof(T)` indirection that `Monitor.Enter` carries. This covers **17 additional lock sites** beyond the Veldrid renderer, including:

- **`LockedWeakList`** — used by the bindable system on every value propagation and by the renderer's live-texture tracking. The `Enumerator` struct uses `Lock.Enter()` / `Lock.Exit()` directly (since `Lock.Scope` is a `ref struct` and cannot be held in a regular struct field).
- **`TextureStore`** — three separate lock objects (`nestedStores`, `textureCache`, `retrievalCompletionSources`), each upgraded to a dedicated `Lock` field.
- **`ThreadRunner`** — `threads` list lock, taken on every `RunMainLoop` iteration.
- **`AudioThread`** — `managers` list lock, taken on every audio thread tick.
- **`ResourceStore`** — `stores` list lock, taken on every resource lookup.
- **`Scheduler`** — `queueLock`, taken every frame on each game thread (was already `Lock`; verified consistent).
- **`AsyncDisposalQueue`**, **`Logger`**, **`LoadingComponentsLogger`**, **`AggregateBindable`**, **`RawCachingGlyphStore`**, **`GlobalStatistics`**, **`HeadlessGameHost.FastClock`** — all lock sites upgraded.

#### Wait-loop improvements

- **`AsyncBufferStream`** (both the background loader and the consumer `Read()` path): `Thread.Sleep(1)` → `SpinWait.SpinOnce()`. The spinner adapts — it spins briefly (no OS yield), then yields to the scheduler, then sleeps, rather than always yielding for a minimum of 1 ms.
- **`GameThreadSynchronizationContext.Send`**: same fix. Cross-thread dispatch (used whenever game-thread work is posted from a non-game thread) no longer has a 1 ms floor.

#### Allocation elimination

- **`AsyncBufferStream`**: `blockLoadedStatus.All(loaded => loaded)` — previously allocated a lambda + LINQ iterator on every single block load — replaced with `Interlocked.Increment(ref loadedBlockCount) == blockLoadedStatus.Length`.
- **`ButtonEventManager.handleButtonUp`**: `.Where(d => d.IsRootedAt(…)).ToList()` (a new `List<Drawable>` on every button-release event) → in-place `RemoveAll`.
- **`ButtonEventManager.handleButtonDown`**: `InputQueue.ToList()` → `new List<Drawable>(InputQueue)` (removes `System.Linq` import entirely from the hot input path).
- **`TimedExpiryCache`**: `DateTimeOffset.Now` → `Environment.TickCount64` for cache-entry expiry tracking (a single `long` read vs. a `DateTimeOffset` struct construction).
- **`GridContainer.distribute`**: `Enumerable.Range(0, n).Where(…).ToArray()` + `cellSizes.Sum()` → two plain `for` loops. No LINQ iterator state machines or lambda delegates on layout passes.
- **`CompositeDrawable` async load scheduling**: `loadables.Any(c => c.IsLongRunning)` → `for` loop with early `break` (no delegate allocation on every `LoadComponentAsync` call).
- **`TabControl`**: `items.ToList()` foreach during tab removal → reverse-index `for` loop (no copy allocation); `SwitchableTabs.Count() < 2` → `!SwitchableTabs.Skip(1).Any()` (stops after finding two elements).
- **`FrameStatisticsDisplay`**: `monitor.ActiveCounters.Any(b => b)` → `Array.IndexOf(…, true) >= 0` (avoids delegate allocation on every statistics poll cycle).
- **`JoystickAxisInput`**: eliminates a double-enumeration of `Count()`.

#### C# / switch modernisation

- `AudioAdjustments`, `AggregateAdjustmentExtensions`, `SampleChannelBass`, `TrackBass`, `GameHost` switch statements → switch expressions (reduced stack frame usage, branch predictor friendlier).
- `NotifyDictionaryChangedEventArgs`: `new[] { item }` → collection expression `[item]` (C# 12).

#### Thread scheduling priorities

Game threads are given OS-level priorities to minimise scheduling jitter and latency:

- **`AudioThread`** → `ThreadPriority.Highest`. BASS's mixer callback is extremely latency-sensitive — even a few milliseconds of preemption causes audible glitches. The audio thread runs at near-zero CPU utilisation between callbacks, so the elevated priority does not starve other threads.
- **`DrawThread`** / **`UpdateThread`** → `ThreadPriority.AboveNormal`. Ensures the render loop and simulation loop are scheduled promptly and are not delayed by lower-priority background work.
- All other `GameThread` subclasses remain at `ThreadPriority.Normal` (no regression).

#### Veldrid `GraphicsPipeline` — draw-call hot path

`DrawVertices` is called hundreds of times per frame (sprites, glyphs, effects). Two dictionary lookups per draw were eliminated:

1. **Texture binding**: `Dictionary<int, VeldridTextureResources>` → `VeldridTextureResources?[16]` flat array. Integer keys are used as direct indices — zero hash computation. A `maxAttachedTextureUnit` high-water mark means the `DrawVertices` loop stops after the highest occupied slot (typically 1–4) rather than scanning the full array. `Array.Clear(16 slots)` in `Begin()` replaces `Dictionary.Clear()`.

2. **Uniform buffer offsets**: the separate `Dictionary<IVeldridUniformBuffer, uint>` that tracked per-buffer offsets was eliminated. The offset is now stored inline as the second field of the `attachedUniformBuffers` value tuple — one fewer identity-hash lookup per UBO per draw call.

#### Deferred rendering list pre-sizing

`DeferredContext.RenderEvents` pre-sized to **4096** entries; `ResourceAllocator.resources` to **512** entries, `memoryBuffers` to **8**. Avoids `List<T>` capacity doubling during first-frame warmup (default capacity = 4, growing to hundreds of entries over the first frame).

#### `ResourceStore<T>` — lock-free snapshot reads

Every `Get()` / `GetStream()` call previously acquired a lock and called `stores.ToArray()` — allocating a fresh array on each resource lookup (texture loads, font glyph fetches, audio file opens). The stores list is now maintained as a `volatile IResourceStore<T>[]` snapshot that is atomically swapped only when `AddStore` / `RemoveStore` is called (rare, startup-only). Hot reads are lock-free.

#### `Logger` — persistent file handle

The log `StreamWriter` is now kept open for the lifetime of the logger. Previously the file handle was opened and closed on every 50 ms scheduler flush tick — adding two syscalls and a heap allocation per tick. The writer is lazily opened on first use, explicitly flushed after each batch, and disposed only on shutdown.

#### Other hot-path wins

- **`GridContainer`** cell sizing uses `RequiredParentSizeToFit` instead of `BoundingBox`, avoiding redundant matrix-to-parent-space transforms on every layout pass ([upstream Issue #3215](https://github.com/ppy/osu-framework/issues/3215)).
- **`VeldridExtensions.LogD3D11`**: removed an unused `ID3D11Device` COM RCW that was being materialised on every device init just to read `FeatureLevel`.
- **`VeldridExtensions.LogOpenGL`**: hoisted cached `Version` / `ShadingLanguageVersion` reads out of the GL-thread execution scope (2 fewer unsafe `glGetString` + `Marshal.PtrToStringUTF8` calls per init).

### ⚙️ .NET 10 upgrade

- All projects target **net10.0** (with `net10.0-android` and `net10.0-ios` for mobile).
- C# 14 language features used throughout, including the `field` keyword for auto-properties (`IDE0032`).
- CI workflows updated for .NET 10 SDK, Xcode 26.3, and Go 1.26.1.

### 📱 Android build configuration

- `SupportedOSPlatformVersion` bumped from **21.0 → 33.0** (Android 13 minimum).
- `AndroidManifest.xml` updated to `minSdkVersion="33"` / `targetSdkVersion="36"`.
- Obsolete `READ_EXTERNAL_STORAGE` permission removed (only applied to API ≤ 32).
- Release config: profiled AOT (`AndroidEnableProfiledAot`), partial trimming, `AndroidStripILAfterAOT=false` (avoids `plt_entry` crashes), `EnableLLVM` removed (incompatible with profiled AOT).
- Native libraries built with **16 KB page alignment** (`-Wl,-z,max-page-size=16384`) for Android 15+ compatibility.
- SDL3 OpenGL surface setup disables `SDL_GL_FRAMEBUFFER_SRGB_CAPABLE` on Android to avoid device-specific colour shifts.

### 🍎 iOS build configuration

- `SupportedOSPlatformVersion` remains **13.4**.
- Trim analysis warnings (`IL2026` / `IL2045` / `IL2060` / `IL2070` / `IL2072` / `IL2075` / `IL2091` / `IL2104`) in framework and test code suppressed with `[DynamicallyAccessedMembers]`, `[UnconditionalSuppressMessage]`, and `<NoWarn>` in `osu.Framework.iOS.props`.

### ✅ Code quality

- All `IDE0032`, `IDE0055`, `IDE0057`, `IDE0042`, `IDE0062`, `IDE0270`, `IDE1006` style warnings resolved.
- CI `CodeFileSanity` step excludes the veldrid / veldrid-spirv submodule directories.
- `EnforceCodeStyleInBuild=true` build passes with **0 warnings, 0 errors** (includes Roslyn analyser rules `IDE0052` etc.).

---

## 📄 Licence

This framework is licensed under the [MIT licence](https://opensource.org/licenses/MIT). Please see [the licence file](LICENCE) for more information. [tl;dr](https://tldrlegal.com/license/mit-license) you can do whatever you want as long as you include the original copyright and license notice in any copy of the software/source.

The BASS audio library (a dependency of this framework) is a commercial product. While it is free for non-commercial use, please ensure to [obtain a valid licence](http://www.un4seen.com/bass.html#license) if you plan on distributing any application using it commercially.

---

## 🎮 Projects that use osu!framework

| Project | Description |
|---------|-------------|
| [osu!](https://github.com/ppy/osu) | Rhythm is just a *click* away! |
| [GDEdit](https://github.com/gd-edit/GDE) | A third-party Geometry Dash editor |
| [Vignette](https://github.com/vignette-project/vignette) | OpenCV-based facial recognition for Live2D |
| [IWBTM](https://github.com/EVAST9919/iwbtm) | Platform game with level editor based on "I Wanna..." games |
| [DeltaDash](https://deltada.sh/) | Multi-direction, lane-based scroller rhythm game |
| [fluXis](https://github.com/TeamFluXis/fluXis) | Community-driven rhythm game with creativity focus |

<!--
We love to see people using our framework! Add your project here via a PR!

Conditions:
 - Must be a GitHub link (i.e. your project is open source)
 - Must be actively developed (and have executable releases)
-->
