using System.Numerics;
using WgpuSharp.Commands;
using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Scene;

/// <summary>
/// Renders a wireframe grid on the XZ plane and colored axis lines at the origin.
/// Features distance-based fade, adaptive subdivision at close range, and colored axes.
/// Designed to render within the same render pass as the scene (called before scene geometry).
/// </summary>
public sealed class EditorGrid : IAsyncDisposable
{
    private readonly GpuDevice _device;
    private GpuRenderPipeline _pipeline = null!;
    private GpuBuffer _vertexBuffer = null!;
    private GpuBuffer _gridUniformBuffer = null!;
    private GpuBindGroup _bindGroup = null!;
    private int _vertexCount;
    private bool _disposed;
    private readonly float[] _gridUniforms = new float[4]; // cameraPos.xyz + pad

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

        // Build grid + axis vertices (larger extent with subdivision lines)
        var verts = BuildGridVertices(gridSize: 50, spacing: 1f);
        _vertexCount = verts.Length / 7; // 7 floats per vertex (pos3 + color4)

        _vertexBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = verts.Length * sizeof(float),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDest,
        }, ct);
        await _vertexBuffer.WriteAsync(verts, ct);

        // Grid-specific uniform buffer for camera position (vec4f = 16 bytes)
        _gridUniformBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = 16,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDest,
        }, ct);

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
            new BindGroupEntry { Binding = 1, Buffer = _gridUniformBuffer, Size = 16 },
        ], ct);
    }

    /// <summary>
    /// Update the grid uniform data with the current camera position.
    /// Must be called each frame before <see cref="Draw"/>.
    /// </summary>
    public void Update(RenderBatch batch, Vector3 cameraPosition)
    {
        _gridUniforms[0] = cameraPosition.X;
        _gridUniforms[1] = cameraPosition.Y;
        _gridUniforms[2] = cameraPosition.Z;
        _gridUniforms[3] = 0f; // padding
        batch.WriteBuffer(_gridUniformBuffer, _gridUniforms);
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

    /// <summary>Line type encoded in the blue channel of the vertex color to distinguish line categories in the shader.</summary>
    private const float LineTypeGrid = 0.0f;
    private const float LineTypeSub = 1.0f;
    private const float LineTypeAxisX = 2.0f;
    private const float LineTypeAxisY = 3.0f;
    private const float LineTypeAxisZ = 4.0f;

    private static float[] BuildGridVertices(int gridSize, float spacing)
    {
        // Each line = 2 vertices, each vertex = 7 floats (pos3 + color4)
        // Color encodes: R=brightness, G=base alpha, B=lineType, A=unused
        var verts = new List<float>();

        float half = gridSize * spacing;

        // --- Main grid lines (1-unit spacing) ---
        for (int i = -gridSize; i <= gridSize; i++)
        {
            if (i == 0) continue; // Center line replaced by axis

            float coord = i * spacing;
            bool isMajor = i % 5 == 0;
            float alpha = isMajor ? 0.25f : 0.12f;

            // Line along X at z=coord
            verts.AddRange([
                -half, 0, coord,   0.5f, alpha, LineTypeGrid, 0,
                 half, 0, coord,   0.5f, alpha, LineTypeGrid, 0,
            ]);

            // Line along Z at x=coord
            verts.AddRange([
                coord, 0, -half,   0.5f, alpha, LineTypeGrid, 0,
                coord, 0,  half,   0.5f, alpha, LineTypeGrid, 0,
            ]);
        }

        // --- Subdivision lines (0.25-unit spacing, only near origin area) ---
        float subSpacing = 0.25f;
        int subRange = 40; // +/- 10 units from origin (in 0.25 steps)
        float subHalf = subRange * subSpacing; // 10 units
        for (int i = -subRange; i <= subRange; i++)
        {
            // Skip positions that coincide with main grid lines (every 4th sub-line = 1.0 spacing)
            if (i % 4 == 0) continue;

            float coord = i * subSpacing;

            // Line along X at z=coord
            verts.AddRange([
                -subHalf, 0, coord,   0.4f, 0.08f, LineTypeSub, 0,
                 subHalf, 0, coord,   0.4f, 0.08f, LineTypeSub, 0,
            ]);

            // Line along Z at x=coord
            verts.AddRange([
                coord, 0, -subHalf,   0.4f, 0.08f, LineTypeSub, 0,
                coord, 0,  subHalf,   0.4f, 0.08f, LineTypeSub, 0,
            ]);
        }

        // --- Axis lines ---
        float axisLen = half;

        // X axis (red)
        verts.AddRange([
            -axisLen, 0, 0,   0.85f, 0.8f, LineTypeAxisX, 0,
             axisLen, 0, 0,   0.85f, 0.8f, LineTypeAxisX, 0,
        ]);

        // Y axis (green)
        verts.AddRange([
            0, 0, 0,          0.85f, 0.8f, LineTypeAxisY, 0,
            0, axisLen, 0,    0.85f, 0.8f, LineTypeAxisY, 0,
        ]);

        // Z axis (blue)
        verts.AddRange([
            0, 0, -axisLen,   0.85f, 0.8f, LineTypeAxisZ, 0,
            0, 0,  axisLen,   0.85f, 0.8f, LineTypeAxisZ, 0,
        ]);

        return verts.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _vertexBuffer.DisposeAsync();
        await _gridUniformBuffer.DisposeAsync();
    }

    private const string GridShaderSource = @"
struct Uniforms {
    viewProj: mat4x4f,
};

struct GridUniforms {
    cameraPos: vec4f,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var<uniform> grid: GridUniforms;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) color: vec4f,  // R=brightness, G=baseAlpha, B=lineType, A=unused
};

struct VertexOutput {
    @builtin(position) clipPos: vec4f,
    @location(0) worldPos: vec3f,
    @location(1) lineData: vec4f,  // R=brightness, G=baseAlpha, B=lineType, A=unused
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.clipPos = uniforms.viewProj * vec4f(input.position, 1.0);
    out.worldPos = input.position;
    out.lineData = input.color;
    return out;
}

@fragment
fn fs_main(@location(0) worldPos: vec3f, @location(1) lineData: vec4f) -> @location(0) vec4f {
    let brightness = lineData.r;
    let baseAlpha = lineData.g;
    let lineType = lineData.b;

    // Distance from camera to this fragment on the XZ plane
    let dx = worldPos.x - grid.cameraPos.x;
    let dz = worldPos.z - grid.cameraPos.z;
    let dist = sqrt(dx * dx + dz * dz);

    // Camera height above the grid plane (used for subdivision visibility)
    let camHeight = abs(grid.cameraPos.y);

    // Determine if this is a subdivision line (lineType ~= 1.0)
    let isSub = step(0.5, lineType) * step(lineType, 1.5);
    // Determine if this is an axis line (lineType >= 2.0)
    let isAxis = step(1.5, lineType);

    // --- Distance-based fade ---
    // Grid and sub lines fade out with distance; axes fade more gently
    let gridFade = 1.0 - smoothstep(20.0, 50.0, dist);
    let axisFade = 1.0 - smoothstep(40.0, 80.0, dist);
    let fade = mix(gridFade, axisFade, isAxis);

    // --- Adaptive subdivision visibility ---
    // Sub lines only visible when camera is close (height < 10 units)
    let subVis = (1.0 - smoothstep(5.0, 12.0, camHeight)) * (1.0 - smoothstep(5.0, 12.0, dist));
    // For non-sub lines, visibility is 1.0; for sub lines, use subVis
    let vis = mix(1.0, subVis, isSub);

    let alpha = baseAlpha * fade * vis;

    // Early discard for nearly-transparent fragments
    if (alpha < 0.004) {
        discard;
    }

    // --- Axis coloring ---
    // lineType 2=X(red), 3=Y(green), 4=Z(blue)
    let isX = step(1.5, lineType) * step(lineType, 2.5);
    let isY = step(2.5, lineType) * step(lineType, 3.5);
    let isZ = step(3.5, lineType) * step(lineType, 4.5);
    let axisColor = vec3f(
        isX * 0.85 + isY * 0.2 + isZ * 0.2,
        isX * 0.2  + isY * 0.85 + isZ * 0.4,
        isX * 0.2  + isY * 0.2 + isZ * 0.85,
    );

    // Grid lines are neutral grey; axis lines use their color
    let gridColor = vec3f(brightness);
    let color = mix(gridColor, axisColor, isAxis);

    return vec4f(color, alpha);
}
";
}
