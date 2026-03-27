using System.Numerics;
using WgpuSharp.Commands;
using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Scene;

/// <summary>
/// Procedural sky gradient rendered as a fullscreen triangle behind all geometry.
/// Blue zenith, warm horizon, dark ground, with a sun disc.
/// </summary>
public sealed class Sky : IAsyncDisposable
{
    private GpuRenderPipeline _pipeline = null!;
    private GpuBuffer _uniformBuffer = null!;
    private GpuBindGroup _bindGroup = null!;
    private readonly byte[] _invVpBytes = new byte[64];
    private bool _disposed;

    /// <summary>Whether to render the sky.</summary>
    public bool Enabled { get; set; } = true;

    private Sky() { }

    /// <summary>Create and initialize the sky renderer.</summary>
    public static async Task<Sky> CreateAsync(GpuDevice device, TextureFormat canvasFormat,
        GpuBuffer envBuffer, int envBufferSize, CancellationToken ct = default)
    {
        var sky = new Sky();
        await sky.InitAsync(device, canvasFormat, envBuffer, envBufferSize, ct);
        return sky;
    }

    private async Task InitAsync(GpuDevice device, TextureFormat canvasFormat,
        GpuBuffer envBuffer, int envBufferSize, CancellationToken ct)
    {
        var shader = await device.CreateShaderModuleAsync(SkyShaderSource, ct);

        _uniformBuffer = await device.CreateBufferAsync(new BufferDescriptor
        {
            Size = 64,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDest,
        }, ct);

        _pipeline = await device.CreateRenderPipelineAsync(new RenderPipelineDescriptor
        {
            Vertex = new VertexState
            {
                Module = shader,
                EntryPoint = "vs_main",
                Buffers = [], // No vertex buffers — fullscreen triangle from vertex ID
            },
            Fragment = new FragmentState
            {
                Module = shader,
                EntryPoint = "fs_main",
                Targets = [new ColorTargetState { Format = canvasFormat }],
            },
            // Depth: write at max depth (1.0), test LessEqual so sky is behind everything
            DepthStencil = new DepthStencilState
            {
                Format = TextureFormat.Depth24Plus,
                DepthWriteEnabled = false,
                DepthCompare = CompareFunction.LessEqual,
            },
        }, ct);

        _bindGroup = await device.CreateBindGroupAsync(_pipeline, 0,
        [
            new BindGroupEntry { Binding = 0, Buffer = _uniformBuffer, Size = 64 },
            new BindGroupEntry { Binding = 1, Buffer = envBuffer, Size = envBufferSize },
        ], ct);
    }

    /// <summary>
    /// Update the sky uniform (inverse view-projection) and draw within a render pass.
    /// Call AFTER clearing but BEFORE scene geometry.
    /// </summary>
    private Matrix4x4 _lastVP;
    private readonly float[] _tempFloats = new float[16];

    public void Update(RenderBatch batch, Matrix4x4 viewProjection)
    {
        if (!Enabled) return;
        if (viewProjection == _lastVP) return; // Skip if camera hasn't moved
        _lastVP = viewProjection;

        if (Matrix4x4.Invert(viewProjection, out var invVP))
        {
            _tempFloats[0] = invVP.M11; _tempFloats[1] = invVP.M12; _tempFloats[2] = invVP.M13; _tempFloats[3] = invVP.M14;
            _tempFloats[4] = invVP.M21; _tempFloats[5] = invVP.M22; _tempFloats[6] = invVP.M23; _tempFloats[7] = invVP.M24;
            _tempFloats[8] = invVP.M31; _tempFloats[9] = invVP.M32; _tempFloats[10] = invVP.M33; _tempFloats[11] = invVP.M34;
            _tempFloats[12] = invVP.M41; _tempFloats[13] = invVP.M42; _tempFloats[14] = invVP.M43; _tempFloats[15] = invVP.M44;
            Buffer.BlockCopy(_tempFloats, 0, _invVpBytes, 0, 64);
            batch.WriteBuffer(_uniformBuffer, _invVpBytes);
        }
    }

    /// <summary>Draw the sky within an existing render pass.</summary>
    public void Draw(BatchedRenderPass pass)
    {
        if (!Enabled) return;
        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _bindGroup);
        pass.Draw(3); // Fullscreen triangle from vertex IDs 0,1,2
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _uniformBuffer.DisposeAsync();
    }

    private const string SkyShaderSource = @"
struct SkyUniforms {
    invViewProj: mat4x4f,
};

struct EnvData {
    sunDirection: vec3f,
    sunIntensity: f32,
    sunColor: vec3f,
    _pad0: f32,
    ambientColor: vec3f,
    _pad1: f32,
    skyZenith: vec3f,
    _pad2: f32,
    skyHorizon: vec3f,
    _pad3: f32,
    skyGround: vec3f,
    _pad4: f32,
};

@group(0) @binding(0) var<uniform> sky: SkyUniforms;
@group(0) @binding(1) var<uniform> env: EnvData;

struct VertexOutput {
    @builtin(position) position: vec4f,
    @location(0) clipPos: vec2f,
};

@vertex
fn vs_main(@builtin(vertex_index) vid: u32) -> VertexOutput {
    let x = f32(i32(vid & 1u)) * 4.0 - 1.0;
    let y = f32(i32(vid >> 1u)) * 4.0 - 1.0;

    var out: VertexOutput;
    out.position = vec4f(x, y, 1.0, 1.0);
    out.clipPos = vec2f(x, y);
    return out;
}

@fragment
fn fs_main(@location(0) clipPos: vec2f) -> @location(0) vec4f {
    let nearWorld = sky.invViewProj * vec4f(clipPos, 0.0, 1.0);
    let farWorld = sky.invViewProj * vec4f(clipPos, 1.0, 1.0);
    let nearPt = nearWorld.xyz / nearWorld.w;
    let farPt = farWorld.xyz / farWorld.w;
    let dir = normalize(farPt - nearPt);

    let y = dir.y;

    // Sky colors from scene settings
    let zenith = env.skyZenith;
    let horizon = env.skyHorizon;
    let ground = env.skyGround;

    var color: vec3f;
    if (y > 0.0) {
        let t = pow(y, 0.5);
        color = mix(horizon, zenith, t);
    } else {
        let t = pow(min(-y, 1.0), 0.7);
        color = mix(horizon * 0.7, ground, t);
    }

    // Sun disc (direction from scene settings)
    let sunDir = normalize(env.sunDirection);
    let sunDot = dot(dir, sunDir);
    if (sunDot > 0.997) {
        color = env.sunColor * 1.2;
    } else if (sunDot > 0.99) {
        let glow = (sunDot - 0.99) / 0.007;
        color = mix(color, env.sunColor, glow * 0.5);
    }

    let hazeStrength = exp(-abs(y) * 4.0) * 0.15;
    color = mix(color, vec3f(0.6, 0.55, 0.5), hazeStrength);

    return vec4f(color, 1.0);
}
";
}
