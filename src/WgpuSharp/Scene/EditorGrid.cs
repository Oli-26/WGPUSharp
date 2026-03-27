using System.Numerics;
using WgpuSharp.Commands;
using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Scene;

/// <summary>
/// Renders a wireframe grid on the XZ plane and colored axis lines at the origin.
/// Designed to render within the same render pass as the scene (called before scene geometry).
/// </summary>
public sealed class EditorGrid : IAsyncDisposable
{
    private readonly GpuDevice _device;
    private GpuRenderPipeline _pipeline = null!;
    private GpuBuffer _vertexBuffer = null!;
    private GpuBindGroup _bindGroup = null!;
    private int _vertexCount;
    private bool _disposed;

    /// <summary>Whether to draw the grid. Toggled from editor UI.</summary>
    public bool Enabled { get; set; } = true;

    private EditorGrid(GpuDevice device) => _device = device;

    /// <summary>
    /// Create and initialize an editor grid.
    /// </summary>
    /// <param name="device">GPU device.</param>
    /// <param name="canvasFormat">Canvas texture format.</param>
    /// <param name="uniformBuffer">The shared uniform buffer containing the view-projection matrix (64 bytes at binding 0).</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<EditorGrid> CreateAsync(GpuDevice device, TextureFormat canvasFormat,
        GpuBuffer uniformBuffer, CancellationToken ct = default)
    {
        var grid = new EditorGrid(device);
        await grid.InitAsync(canvasFormat, uniformBuffer, ct);
        return grid;
    }

    private async Task InitAsync(TextureFormat canvasFormat, GpuBuffer uniformBuffer, CancellationToken ct)
    {
        var shader = await _device.CreateShaderModuleAsync(GridShaderSource, ct);

        // Build grid + axis vertices
        var verts = BuildGridVertices(gridSize: 20, spacing: 1f);
        _vertexCount = verts.Length / 7; // 7 floats per vertex (pos3 + color4)

        _vertexBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = verts.Length * sizeof(float),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDest,
        }, ct);
        await _vertexBuffer.WriteAsync(verts, ct);

        _pipeline = await _device.CreateRenderPipelineAsync(new RenderPipelineDescriptor
        {
            Vertex = new VertexState
            {
                Module = shader,
                EntryPoint = "vs_main",
                Buffers =
                [
                    new VertexBufferLayout
                    {
                        ArrayStride = 7 * sizeof(float), // pos(3) + color(4)
                        StepMode = VertexStepMode.Vertex,
                        Attributes =
                        [
                            new VertexAttribute { ShaderLocation = 0, Offset = 0, Format = VertexFormat.Float32x3 },
                            new VertexAttribute { ShaderLocation = 1, Offset = 3 * sizeof(float), Format = VertexFormat.Float32x4 },
                        ],
                    },
                ],
            },
            Fragment = new FragmentState
            {
                Module = shader,
                EntryPoint = "fs_main",
                Targets = [new ColorTargetState
                {
                    Format = canvasFormat,
                    Blend = BlendState.AlphaBlend,
                }],
            },
            DepthStencil = new DepthStencilState
            {
                Format = TextureFormat.Depth24Plus,
                DepthWriteEnabled = true,
                DepthCompare = CompareFunction.Less,
            },
            PrimitiveTopology = PrimitiveTopology.LineList,
        }, ct);

        _bindGroup = await _device.CreateBindGroupAsync(_pipeline, 0,
        [
            new BindGroupEntry { Binding = 0, Buffer = uniformBuffer, Size = 64 },
        ], ct);
    }

    /// <summary>
    /// Draw the grid within an existing render pass.
    /// Call this before drawing scene objects so the grid sits behind them.
    /// </summary>
    public void Draw(BatchedRenderPass pass)
    {
        if (!Enabled) return;

        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _bindGroup);
        pass.SetVertexBuffer(0, _vertexBuffer);
        pass.Draw(_vertexCount);
    }

    private static float[] BuildGridVertices(int gridSize, float spacing)
    {
        // Each line = 2 vertices, each vertex = 7 floats (pos3 + color4)
        var verts = new List<float>();

        float half = gridSize * spacing;
        float gridAlpha = 0.25f;
        float subGridAlpha = 0.12f;

        // Grid lines along X (varying Z)
        for (int i = -gridSize; i <= gridSize; i++)
        {
            float z = i * spacing;
            bool isCenter = i == 0;
            float alpha = isCenter ? 0f : (i % 5 == 0 ? gridAlpha : subGridAlpha);

            if (isCenter) continue; // Center line replaced by axis

            // Line from (-half, 0, z) to (half, 0, z)
            verts.AddRange([
                -half, 0, z,   0.5f, 0.5f, 0.5f, alpha,
                 half, 0, z,   0.5f, 0.5f, 0.5f, alpha,
            ]);
        }

        // Grid lines along Z (varying X)
        for (int i = -gridSize; i <= gridSize; i++)
        {
            float x = i * spacing;
            bool isCenter = i == 0;
            float alpha = isCenter ? 0f : (i % 5 == 0 ? gridAlpha : subGridAlpha);

            if (isCenter) continue;

            // Line from (x, 0, -half) to (x, 0, half)
            verts.AddRange([
                x, 0, -half,   0.5f, 0.5f, 0.5f, alpha,
                x, 0,  half,   0.5f, 0.5f, 0.5f, alpha,
            ]);
        }

        float axisLen = half;
        float axisAlpha = 0.8f;

        // X axis (red) — from -half to +half along X at Z=0
        verts.AddRange([
            -axisLen, 0, 0,   0.85f, 0.2f, 0.2f, axisAlpha,
             axisLen, 0, 0,   0.85f, 0.2f, 0.2f, axisAlpha,
        ]);

        // Y axis (green) — from 0 to +axisLen along Y at origin
        verts.AddRange([
            0, 0, 0,          0.2f, 0.85f, 0.2f, axisAlpha,
            0, axisLen, 0,    0.2f, 0.85f, 0.2f, axisAlpha,
        ]);

        // Z axis (blue) — from -half to +half along Z at X=0
        verts.AddRange([
            0, 0, -axisLen,   0.2f, 0.4f, 0.85f, axisAlpha,
            0, 0,  axisLen,   0.2f, 0.4f, 0.85f, axisAlpha,
        ]);

        return verts.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _vertexBuffer.DisposeAsync();
    }

    private const string GridShaderSource = @"
struct Uniforms {
    viewProj: mat4x4f,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) color: vec4f,
};

struct VertexOutput {
    @builtin(position) clipPos: vec4f,
    @location(0) color: vec4f,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.clipPos = uniforms.viewProj * vec4f(input.position, 1.0);
    out.color = input.color;
    return out;
}

@fragment
fn fs_main(@location(0) color: vec4f) -> @location(0) vec4f {
    return color;
}
";
}
