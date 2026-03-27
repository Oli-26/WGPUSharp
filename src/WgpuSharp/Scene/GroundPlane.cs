using WgpuSharp.Commands;
using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Scene;

/// <summary>
/// A large lit ground plane at Y=0 with a procedural checkerboard pattern.
/// Receives point light illumination via the shared light uniform buffer.
/// Renders as a single quad through its own pipeline with a procedural fragment shader.
/// </summary>
public sealed class GroundPlane : IAsyncDisposable
{
    private GpuRenderPipeline _pipeline = null!;
    private GpuBuffer _vertexBuffer = null!;
    private GpuBindGroup _bindGroup = null!;
    private bool _disposed;

    /// <summary>Whether to render the ground plane.</summary>
    public bool Enabled { get; set; } = true;

    private GroundPlane() { }

    /// <summary>Create and initialize the ground plane.</summary>
    public static async Task<GroundPlane> CreateAsync(GpuDevice device, TextureFormat canvasFormat,
        GpuBuffer uniformBuffer, GpuBuffer lightBuffer, int lightBufferSize,
        GpuBuffer envBuffer, int envBufferSize, CancellationToken ct = default)
    {
        var gp = new GroundPlane();
        await gp.InitAsync(device, canvasFormat, uniformBuffer, lightBuffer, lightBufferSize, envBuffer, envBufferSize, ct);
        return gp;
    }

    private async Task InitAsync(GpuDevice device, TextureFormat canvasFormat,
        GpuBuffer uniformBuffer, GpuBuffer lightBuffer, int lightBufferSize,
        GpuBuffer envBuffer, int envBufferSize, CancellationToken ct)
    {
        var shader = await device.CreateShaderModuleAsync(ShaderSource, ct);

        // Large quad: 6 vertices (2 triangles), each with pos(3) + normal(3)
        const float size = 50f;
        float[] verts =
        [
            // Triangle 1
            -size, 0, -size,  0, 1, 0,
             size, 0, -size,  0, 1, 0,
             size, 0,  size,  0, 1, 0,
            // Triangle 2
            -size, 0, -size,  0, 1, 0,
             size, 0,  size,  0, 1, 0,
            -size, 0,  size,  0, 1, 0,
        ];

        _vertexBuffer = await device.CreateBufferAsync(new BufferDescriptor
        {
            Size = verts.Length * sizeof(float),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDest,
        }, ct);
        await _vertexBuffer.WriteAsync(verts, ct);

        _pipeline = await device.CreateRenderPipelineAsync(new RenderPipelineDescriptor
        {
            Vertex = new VertexState
            {
                Module = shader,
                EntryPoint = "vs_main",
                Buffers =
                [
                    new VertexBufferLayout
                    {
                        ArrayStride = 6 * sizeof(float),
                        StepMode = VertexStepMode.Vertex,
                        Attributes =
                        [
                            new VertexAttribute { ShaderLocation = 0, Offset = 0, Format = VertexFormat.Float32x3 },
                            new VertexAttribute { ShaderLocation = 1, Offset = 3 * sizeof(float), Format = VertexFormat.Float32x3 },
                        ],
                    },
                ],
            },
            Fragment = new FragmentState
            {
                Module = shader,
                EntryPoint = "fs_main",
                Targets = [new ColorTargetState { Format = canvasFormat, Blend = BlendState.AlphaBlend }],
            },
            DepthStencil = new DepthStencilState
            {
                Format = TextureFormat.Depth24Plus,
                DepthWriteEnabled = true,
                DepthCompare = CompareFunction.Less,
            },
        }, ct);

        _bindGroup = await device.CreateBindGroupAsync(_pipeline, 0,
        [
            new BindGroupEntry { Binding = 0, Buffer = uniformBuffer, Size = 64 },
            new BindGroupEntry { Binding = 1, Buffer = lightBuffer, Size = lightBufferSize },
            new BindGroupEntry { Binding = 2, Buffer = envBuffer, Size = envBufferSize },
        ], ct);
    }

    /// <summary>Draw the ground plane within an existing render pass.</summary>
    public void Draw(BatchedRenderPass pass)
    {
        if (!Enabled) return;
        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _bindGroup);
        pass.SetVertexBuffer(0, _vertexBuffer);
        pass.Draw(6);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _vertexBuffer.DisposeAsync();
    }

    private const string ShaderSource = @"
struct Uniforms {
    viewProj: mat4x4f,
};

struct PointLight {
    position: vec3f,
    _pad0: f32,
    color: vec3f,
    intensity: f32,
};

struct LightData {
    count: u32,
    _pad0: u32,
    _pad1: u32,
    _pad2: u32,
    lights: array<PointLight, 8>,
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
    fogColor: vec3f,
    fogStart: f32,
    fogEnd: f32,
    _pad5: f32,
    _pad6: f32,
    _pad7: f32,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var<uniform> lightData: LightData;
@group(0) @binding(2) var<uniform> env: EnvData;

struct VertexOutput {
    @builtin(position) clipPos: vec4f,
    @location(0) worldPos: vec3f,
    @location(1) normal: vec3f,
};

@vertex
fn vs_main(@location(0) pos: vec3f, @location(1) normal: vec3f) -> VertexOutput {
    var out: VertexOutput;
    out.clipPos = uniforms.viewProj * vec4f(pos, 1.0);
    out.worldPos = pos;
    out.normal = normal;
    return out;
}

@fragment
fn fs_main(@location(0) worldPos: vec3f, @location(1) normal: vec3f) -> @location(0) vec4f {
    let n = normalize(normal);

    // Checkerboard pattern from world XZ
    let checker = floor(worldPos.x) + floor(worldPos.z);
    let isLight = (i32(checker) & 1) == 0;
    let baseColor = select(vec3f(0.18, 0.18, 0.20), vec3f(0.22, 0.22, 0.25), isLight);

    // Ambient (from scene settings)
    var lighting = env.ambientColor * 0.85;

    // Directional sun (from scene settings, subtler on ground)
    let sunDir = normalize(env.sunDirection);
    lighting += env.sunColor * max(dot(n, sunDir), 0.0) * env.sunIntensity * 0.7;

    // Point lights
    let count = min(lightData.count, 8u);
    for (var i = 0u; i < count; i++) {
        let light = lightData.lights[i];
        let toLight = light.position - worldPos;
        let dist = length(toLight);
        let dir = toLight / max(dist, 0.001);
        let atten = light.intensity / (1.0 + dist * dist * 0.2);
        let ndotl = max(dot(n, dir), 0.0);
        lighting += light.color * ndotl * atten;
    }

    // Fade out at edges (distance from origin)
    let distFromCenter = length(worldPos.xz);
    let fade = 1.0 - smoothstep(30.0, 50.0, distFromCenter);

    let color = baseColor * lighting;

    // Distance fog
    let fogDist = length(worldPos);
    let fogFactor = clamp((fogDist - env.fogStart) / (env.fogEnd - env.fogStart), 0.0, 1.0);
    let finalColor = mix(color, env.fogColor, fogFactor);
    return vec4f(finalColor, fade);
}
";
}
