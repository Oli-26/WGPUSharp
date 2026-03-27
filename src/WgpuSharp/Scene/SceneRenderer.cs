using System.Numerics;
using WgpuSharp.Commands;
using WgpuSharp.Core;
using WgpuSharp.Mesh;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Scene;

/// <summary>
/// Renders a <see cref="Scene"/> using the batched command system.
/// Handles instanced rendering grouped by mesh, directional lighting, and selection highlighting.
/// </summary>
public sealed class SceneRenderer : IAsyncDisposable
{
    private readonly GpuDevice _device;
    private GpuRenderPipeline _pipeline = null!;
    private GpuBuffer _uniformBuffer = null!;
    private GpuBuffer _lightBuffer = null!;
    private GpuBuffer _envBuffer = null!;
    private GpuBuffer _instanceBuffer = null!;
    private GpuBindGroup _bindGroup = null!;
    private int _maxInstances;
    private bool _disposed;

    private const int MaxLights = 8;
    private const int LightBufferSize = 16 + MaxLights * 32;
    private const int EnvBufferSize = 128;

    // Pre-allocated buffers (reused every frame — zero allocation render loop)
    private byte[] _vpBytes = new byte[64];
    private byte[] _lightBytes = new byte[LightBufferSize];
    private float[] _instanceData = [];
    private Matrix4x4 _currentVP;
    private readonly Dictionary<MeshBuffers, List<SceneNode>> _groups = new();
    private readonly List<(MeshBuffers mesh, int firstInstance, int instanceCount)> _drawCalls = new();

    private SceneRenderer(GpuDevice device) => _device = device;

    /// <summary>The clear color used for the background (visible if sky is disabled).</summary>
    public GpuColor ClearColor { get; set; } = new(0.08, 0.08, 0.10, 1.0);

    /// <summary>Editor grid overlay. Null if not initialized.</summary>
    public EditorGrid? Grid { get; private set; }

    /// <summary>Translation gizmo for the selected object.</summary>
    public TranslateGizmo? Gizmo { get; private set; }

    /// <summary>Lit ground plane at Y=0 with checkerboard pattern.</summary>
    public GroundPlane? Ground { get; private set; }

    /// <summary>Procedural sky gradient.</summary>
    public Sky? Sky { get; private set; }

    /// <summary>
    /// Create and initialize a scene renderer for the given device and canvas format.
    /// </summary>
    public static async Task<SceneRenderer> CreateAsync(GpuDevice device, TextureFormat canvasFormat, int maxInstances = 1024, CancellationToken ct = default)
    {
        var renderer = new SceneRenderer(device);
        await renderer.InitAsync(canvasFormat, maxInstances, ct);
        return renderer;
    }

    private async Task InitAsync(TextureFormat canvasFormat, int maxInstances, CancellationToken ct)
    {
        _maxInstances = maxInstances;

        var shader = await _device.CreateShaderModuleAsync(ShaderSource, ct);

        _uniformBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = 64,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDest,
        }, ct);

        _lightBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = LightBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDest,
        }, ct);

        _envBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = EnvBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDest,
        }, ct);

        // 80 bytes per instance: model matrix (64) + color (16)
        _instanceBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = maxInstances * 80,
            Usage = BufferUsage.Vertex | BufferUsage.CopyDest,
        }, ct);

        _instanceData = new float[maxInstances * 20]; // 20 floats per instance

        _pipeline = await _device.CreateRenderPipelineAsync(new RenderPipelineDescriptor
        {
            Vertex = new VertexState
            {
                Module = shader,
                EntryPoint = "vs_main",
                Buffers =
                [
                    // Slot 0: Mesh vertex data (pos + normal)
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
                    // Slot 1: Instance data (model matrix + color)
                    new VertexBufferLayout
                    {
                        ArrayStride = 80,
                        StepMode = VertexStepMode.Instance,
                        Attributes =
                        [
                            new VertexAttribute { ShaderLocation = 3, Offset = 0, Format = VertexFormat.Float32x4 },
                            new VertexAttribute { ShaderLocation = 4, Offset = 16, Format = VertexFormat.Float32x4 },
                            new VertexAttribute { ShaderLocation = 5, Offset = 32, Format = VertexFormat.Float32x4 },
                            new VertexAttribute { ShaderLocation = 6, Offset = 48, Format = VertexFormat.Float32x4 },
                            new VertexAttribute { ShaderLocation = 7, Offset = 64, Format = VertexFormat.Float32x4 },
                        ],
                    },
                ],
            },
            Fragment = new FragmentState
            {
                Module = shader,
                EntryPoint = "fs_main",
                Targets = [new ColorTargetState { Format = canvasFormat }],
            },
            DepthStencil = new DepthStencilState
            {
                Format = TextureFormat.Depth24Plus,
                DepthWriteEnabled = true,
                DepthCompare = CompareFunction.Less,
            },
            CullMode = CullMode.Back,
        }, ct);

        _bindGroup = await _device.CreateBindGroupAsync(_pipeline, 0,
        [
            new BindGroupEntry { Binding = 0, Buffer = _uniformBuffer, Size = 64 },
            new BindGroupEntry { Binding = 1, Buffer = _lightBuffer, Size = LightBufferSize },
            new BindGroupEntry { Binding = 2, Buffer = _envBuffer, Size = EnvBufferSize },
        ], ct);

        // Editor overlays share buffers; ground plane and sky need env buffer for configurable lighting
        Grid = await EditorGrid.CreateAsync(_device, canvasFormat, _uniformBuffer, ct);
        Gizmo = await TranslateGizmo.CreateAsync(_device, canvasFormat, _uniformBuffer, ct);
        Ground = await GroundPlane.CreateAsync(_device, canvasFormat, _uniformBuffer, _lightBuffer, LightBufferSize, _envBuffer, EnvBufferSize, ct);
        Sky = await Sky.CreateAsync(_device, canvasFormat, _envBuffer, EnvBufferSize, ct);
    }

    /// <summary>
    /// Render the scene into a RenderBatch using an orbit camera.
    /// </summary>
    public void Render(RenderBatch batch, Scene scene, OrbitCamera camera, GpuCanvas canvas,
        BatchedTextureView colorView, GpuTextureView depthView, SceneSettings settings, HashSet<int>? selectedNodeIds = null)
    {
        _currentVP = camera.ViewProjectionMatrix(canvas.AspectRatio);
        camera.WriteViewProjection(canvas.AspectRatio, _vpBytes);
        RenderWithVP(batch, scene, camera.Position, canvas, colorView, depthView, settings, selectedNodeIds);
    }

    /// <summary>
    /// Render the scene into a RenderBatch using an FPS camera (play mode).
    /// Hides the editor grid and gizmo.
    /// </summary>
    public void Render(RenderBatch batch, Scene scene, FpsCamera camera, GpuCanvas canvas,
        BatchedTextureView colorView, GpuTextureView depthView, SceneSettings settings)
    {
        _currentVP = camera.ViewProjectionMatrix(canvas.AspectRatio);
        camera.WriteViewProjection(canvas.AspectRatio, _vpBytes);
        RenderWithVP(batch, scene, camera.Position, canvas, colorView, depthView, settings, selectedNodeIds: null, showEditorOverlays: false);
    }

    private void RenderWithVP(RenderBatch batch, Scene scene, Vector3 cameraPosition, GpuCanvas canvas,
        BatchedTextureView colorView, GpuTextureView depthView, SceneSettings settings,
        HashSet<int>? selectedNodeIds = null, bool showEditorOverlays = true)
    {
        batch.WriteBuffer(_uniformBuffer, _vpBytes);

        // Collect and write light data
        WriteLightData(scene);
        batch.WriteBuffer(_lightBuffer, _lightBytes);

        // Write environment settings
        batch.WriteBuffer(_envBuffer, settings.ToBytes());

        // Collect visible mesh nodes, resolve LOD by distance, group by resolved mesh
        _groups.Clear();
        foreach (var node in scene.GetVisibleMeshNodes())
        {
            float dist = Vector3.Distance(cameraPosition, node.Transform.WorldMatrix.Translation);
            var mesh = node.GetLodMesh(dist) ?? node.MeshBuffers;
            if (mesh is null) continue;
            if (!_groups.TryGetValue(mesh, out var list))
            {
                list = [];
                _groups[mesh] = list;
            }
            list.Add(node);
        }

        // Write instance data for all groups
        int totalInstances = 0;
        _drawCalls.Clear();

        foreach (var (mesh, nodes) in _groups)
        {
            int firstInstance = totalInstances;
            foreach (var node in nodes)
            {
                if (totalInstances >= _maxInstances) break;
                if (!showEditorOverlays && (node.Tag == NodeTag.Trigger || node.Tag == NodeTag.Checkpoint)) continue;

                var m = node.Transform.WorldMatrix;
                int offset = totalInstances * 20;

                // Column-major matrix (matches WGSL mat4x4f constructor from 4x vec4f)
                _instanceData[offset + 0] = m.M11; _instanceData[offset + 1] = m.M12;
                _instanceData[offset + 2] = m.M13; _instanceData[offset + 3] = m.M14;
                _instanceData[offset + 4] = m.M21; _instanceData[offset + 5] = m.M22;
                _instanceData[offset + 6] = m.M23; _instanceData[offset + 7] = m.M24;
                _instanceData[offset + 8] = m.M31; _instanceData[offset + 9] = m.M32;
                _instanceData[offset + 10] = m.M33; _instanceData[offset + 11] = m.M34;
                _instanceData[offset + 12] = m.M41; _instanceData[offset + 13] = m.M42;
                _instanceData[offset + 14] = m.M43; _instanceData[offset + 15] = m.M44;

                // Color (highlight selected nodes)
                var color = node.Color;
                bool isSelected = selectedNodeIds is not null && selectedNodeIds.Contains(node.Id);
                if (isSelected)
                {
                    // Orange tint for selection
                    color = new Vector4(
                        MathF.Min(1f, color.X + 0.3f),
                        MathF.Min(1f, color.Y + 0.15f),
                        color.Z * 0.7f,
                        color.W
                    );
                }

                _instanceData[offset + 16] = color.X;
                _instanceData[offset + 17] = color.Y;
                _instanceData[offset + 18] = color.Z;
                _instanceData[offset + 19] = color.W;

                totalInstances++;
            }

            int count = totalInstances - firstInstance;
            if (count > 0)
                _drawCalls.Add((mesh, firstInstance, count));
        }

        if (totalInstances > 0)
        {
            // Write only the used portion of instance data
            // Write the full pre-allocated array — GPU only reads totalInstances worth
            batch.WriteBuffer(_instanceBuffer, _instanceData);
        }

        // Single render pass
        var encoder = batch.CreateEncoder();
        var pass = encoder.BeginRenderPass(colorView, ClearColor, depthView);

        // Sky (renders at far depth, behind everything)
        if (Sky is not null)
        {
            Sky.Update(batch, _currentVP);
            Sky.Draw(pass);
        }

        // Draw editor grid (only in editor mode)
        if (showEditorOverlays) Grid?.Draw(pass);

        // Draw lit ground plane (editor + play mode)
        Ground?.Draw(pass);

        // Draw scene objects
        if (totalInstances > 0)
        {
            pass.SetPipeline(_pipeline);
            pass.SetBindGroup(0, _bindGroup);
            pass.SetVertexBuffer(1, _instanceBuffer);

            foreach (var (mesh, firstInstance, instanceCount) in _drawCalls)
            {
                pass.SetVertexBuffer(0, mesh.VertexBuffer);
                if (mesh.IndexBuffer is not null)
                {
                    pass.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.Uint32);
                    pass.DrawIndexed(mesh.IndexCount, instanceCount, 0, 0, firstInstance);
                }
                else
                {
                    pass.Draw(mesh.VertexCount, instanceCount, 0, firstInstance);
                }
            }
        }

        // Draw translation gizmo on selected object (editor mode only)
        if (showEditorOverlays && Gizmo is not null && selectedNodeIds is { Count: > 0 })
        {
            var selectedNode = scene.FindById(selectedNodeIds.First());
            if (selectedNode is not null)
            {
                var gizmoPos = selectedNode.Transform.WorldMatrix.Translation;
                float camDist = Vector3.Distance(cameraPosition, gizmoPos);
                Gizmo.Update(batch, gizmoPos, camDist);
                Gizmo.Draw(pass);
            }
        }

        pass.End();
        encoder.Submit();
    }

    private void WriteLightData(Scene scene)
    {
        Array.Clear(_lightBytes);
        int count = 0;

        foreach (var node in scene.GetLightNodes())
        {
            if (count >= MaxLights) break;
            var light = node.Light!;
            var pos = node.Transform.WorldMatrix.Translation;

            int offset = 16 + count * 32; // 16-byte header, 32 bytes per light
            BitConverter.TryWriteBytes(_lightBytes.AsSpan(offset), pos.X);
            BitConverter.TryWriteBytes(_lightBytes.AsSpan(offset + 4), pos.Y);
            BitConverter.TryWriteBytes(_lightBytes.AsSpan(offset + 8), pos.Z);
            // offset + 12: padding
            BitConverter.TryWriteBytes(_lightBytes.AsSpan(offset + 16), light.Color.X);
            BitConverter.TryWriteBytes(_lightBytes.AsSpan(offset + 20), light.Color.Y);
            BitConverter.TryWriteBytes(_lightBytes.AsSpan(offset + 24), light.Color.Z);
            BitConverter.TryWriteBytes(_lightBytes.AsSpan(offset + 28), light.Intensity);

            count++;
        }

        // Write count at offset 0
        BitConverter.TryWriteBytes(_lightBytes.AsSpan(0), (uint)count);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (Grid is not null) await Grid.DisposeAsync();
        if (Gizmo is not null) await Gizmo.DisposeAsync();
        if (Ground is not null) await Ground.DisposeAsync();
        if (Sky is not null) await Sky.DisposeAsync();
        await _uniformBuffer.DisposeAsync();
        await _lightBuffer.DisposeAsync();
        await _envBuffer.DisposeAsync();
        await _instanceBuffer.DisposeAsync();
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

struct InstanceData {
    @location(3) model0: vec4f,
    @location(4) model1: vec4f,
    @location(5) model2: vec4f,
    @location(6) model3: vec4f,
    @location(7) color: vec4f,
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
    @builtin(position) position: vec4f,
    @location(0) normal: vec3f,
    @location(1) color: vec3f,
    @location(2) worldPos: vec3f,
};

@vertex
fn vs_main(
    @location(0) pos: vec3f,
    @location(1) normal: vec3f,
    inst: InstanceData,
) -> VertexOutput {
    let model = mat4x4f(inst.model0, inst.model1, inst.model2, inst.model3);
    let worldPos = model * vec4f(pos, 1.0);

    var out: VertexOutput;
    out.position = uniforms.viewProj * worldPos;
    out.normal = normalize((model * vec4f(normal, 0.0)).xyz);
    out.color = inst.color.rgb;
    out.worldPos = worldPos.xyz;
    return out;
}

@fragment
fn fs_main(
    @location(0) normal: vec3f,
    @location(1) color: vec3f,
    @location(2) worldPos: vec3f,
) -> @location(0) vec4f {
    let n = normalize(normal);

    // Ambient base (from scene settings)
    var lighting = env.ambientColor;

    // Directional sun light (from scene settings)
    let sunDir = normalize(env.sunDirection);
    lighting += env.sunColor * max(dot(n, sunDir), 0.0) * env.sunIntensity;

    // Point lights
    let count = min(lightData.count, 8u);
    for (var i = 0u; i < count; i++) {
        let light = lightData.lights[i];
        let toLight = light.position - worldPos;
        let dist = length(toLight);
        let dir = toLight / max(dist, 0.001);

        // Smooth attenuation
        let atten = light.intensity / (1.0 + dist * dist * 0.2);
        let ndotl = max(dot(n, dir), 0.0);
        lighting += light.color * ndotl * atten;
    }

    // Distance fog
    let fogDist = length(worldPos);
    let fogFactor = clamp((fogDist - env.fogStart) / (env.fogEnd - env.fogStart), 0.0, 1.0);
    let finalColor = mix(color * lighting, env.fogColor, fogFactor);
    return vec4f(finalColor, 1.0);
}
";
}
