using System.Numerics;
using WgpuSharp.Commands;
using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Scene;

/// <summary>
/// Which axis (or plane) the gizmo is operating on.
/// </summary>
public enum GizmoAxis
{
    None,
    X,
    Y,
    Z,
}

/// <summary>
/// Active gizmo tool mode.
/// </summary>
public enum GizmoMode
{
    /// <summary>W key — move along axis.</summary>
    Translate,
    /// <summary>E key — rotate around axis.</summary>
    Rotate,
    /// <summary>R key — scale along axis.</summary>
    Scale,
}

/// <summary>
/// Multi-mode gizmo (Translate / Rotate / Scale) rendered at a selected object's position.
/// Three colored axis arrows (R=X, G=Y, B=Z). Drag behavior depends on <see cref="Mode"/>.
/// </summary>
public sealed class TranslateGizmo : IAsyncDisposable
{
    private readonly GpuDevice _device;
    private GpuRenderPipeline _pipeline = null!;
    private GpuBuffer _vertexBuffer = null!;
    private GpuBindGroup _bindGroup = null!;
    private bool _disposed;

    // Vertex budget: thick arrows (3 parallel lines * 3 axes * 6 verts = 54, arrowheads ~30)
    // + rotation rings (48*2*3) + highlight ring (48*2) + sweep arc (48*2) + scale axes (~84)
    private const int RingSegments = 48;
    private const int MaxVertices = 900;
    private const int FloatsPerVertex = 7; // pos(3) + color(4)
    private readonly float[] _vertexData = new float[MaxVertices * FloatsPerVertex];
    private int _drawVertexCount;

    /// <summary>Whether the gizmo is visible.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Current tool mode (Translate, Rotate, Scale).</summary>
    public GizmoMode Mode { get; set; } = GizmoMode.Translate;

    /// <summary>Currently hovered/active axis (for highlighting).</summary>
    public GizmoAxis HoveredAxis { get; set; }

    /// <summary>Currently dragging axis.</summary>
    public GizmoAxis DragAxis { get; private set; }

    /// <summary>Whether a drag is in progress.</summary>
    public bool IsDragging => DragAxis != GizmoAxis.None;

    // Drag state — shared
    private Vector3 _dragStartPos;
    private Vector3 _dragAxisDir;
    private float _dragStartProjection;

    // Rotate drag state
    private Quaternion _dragStartRotation;
    private Vector3 _dragStartPlaneDir; // direction from center to initial hit on ring plane

    /// <summary>Current drag angle in radians (for visual arc feedback).</summary>
    public float DragAngle { get; private set; }

    // Scale drag state
    private Vector3 _dragStartScale;

    private TranslateGizmo(GpuDevice device) => _device = device;

    /// <summary>Create and initialize the translate gizmo.</summary>
    public static async Task<TranslateGizmo> CreateAsync(GpuDevice device, TextureFormat canvasFormat,
        GpuBuffer uniformBuffer, CancellationToken ct = default)
    {
        var gizmo = new TranslateGizmo(device);
        await gizmo.InitAsync(canvasFormat, uniformBuffer, ct);
        return gizmo;
    }

    private async Task InitAsync(TextureFormat canvasFormat, GpuBuffer uniformBuffer, CancellationToken ct)
    {
        var shader = await _device.CreateShaderModuleAsync(GizmoShaderSource, ct);

        _vertexBuffer = await _device.CreateBufferAsync(new BufferDescriptor
        {
            Size = MaxVertices * FloatsPerVertex * sizeof(float),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDest,
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
                        ArrayStride = FloatsPerVertex * sizeof(float),
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
                Targets = [new ColorTargetState { Format = canvasFormat }],
            },
            // Depth: always pass (renders on top), no depth write
            DepthStencil = new DepthStencilState
            {
                Format = TextureFormat.Depth24Plus,
                DepthWriteEnabled = false,
                DepthCompare = CompareFunction.Always,
            },
            PrimitiveTopology = PrimitiveTopology.LineList,
        }, ct);

        _bindGroup = await _device.CreateBindGroupAsync(_pipeline, 0,
        [
            new BindGroupEntry { Binding = 0, Buffer = uniformBuffer, Size = 64 },
        ], ct);
    }

    /// <summary>
    /// Update gizmo vertex data for the given world position and write to the batch.
    /// Call before Draw().
    /// </summary>
    private Vector3 _lastWorldPos;
    private float _lastCamDist;
    private GizmoAxis _lastHovered;
    private GizmoMode _lastMode;

    public void Update(RenderBatch batch, Vector3 worldPos, float cameraDistance)
    {
        // Skip rebuild if nothing changed
        bool needsUpdate = worldPos != _lastWorldPos
            || MathF.Abs(cameraDistance - _lastCamDist) > 0.01f
            || HoveredAxis != _lastHovered
            || Mode != _lastMode
            || IsDragging;
        if (!needsUpdate) return;
        _lastWorldPos = worldPos;
        _lastCamDist = cameraDistance;
        _lastHovered = HoveredAxis;
        _lastMode = Mode;

        // Scale gizmo so it's a consistent screen size regardless of zoom
        float size = cameraDistance * 0.18f;
        float arrowSize = size * 0.18f;

        int vi = 0;
        switch (Mode)
        {
            case GizmoMode.Translate:
                WriteTranslateAxis(ref vi, worldPos, Vector3.UnitX, size, arrowSize, GizmoAxis.X);
                WriteTranslateAxis(ref vi, worldPos, Vector3.UnitY, size, arrowSize, GizmoAxis.Y);
                WriteTranslateAxis(ref vi, worldPos, Vector3.UnitZ, size, arrowSize, GizmoAxis.Z);
                break;
            case GizmoMode.Rotate:
                WriteRotateRing(ref vi, worldPos, Vector3.UnitX, size, GizmoAxis.X);
                WriteRotateRing(ref vi, worldPos, Vector3.UnitY, size, GizmoAxis.Y);
                WriteRotateRing(ref vi, worldPos, Vector3.UnitZ, size, GizmoAxis.Z);
                // Draw sweep arc for active drag
                if (IsDragging && MathF.Abs(DragAngle) > 0.01f)
                    WriteSweepArc(ref vi, worldPos, _dragAxisDir, size, DragAxis);
                break;
            case GizmoMode.Scale:
                WriteScaleAxis(ref vi, worldPos, Vector3.UnitX, size, arrowSize, GizmoAxis.X);
                WriteScaleAxis(ref vi, worldPos, Vector3.UnitY, size, arrowSize, GizmoAxis.Y);
                WriteScaleAxis(ref vi, worldPos, Vector3.UnitZ, size, arrowSize, GizmoAxis.Z);
                break;
        }
        _drawVertexCount = vi;

        batch.WriteBuffer(_vertexBuffer, _vertexData, _drawVertexCount * FloatsPerVertex);
    }

    /// <summary>Draw the gizmo within an existing render pass.</summary>
    public void Draw(BatchedRenderPass pass)
    {
        if (!Enabled) return;

        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _bindGroup);
        pass.SetVertexBuffer(0, _vertexBuffer);
        pass.Draw(_drawVertexCount);
    }

    /// <summary>
    /// Test if a click ray hits one of the gizmo axes.
    /// Returns the hit axis, or None.
    /// </summary>
    public GizmoAxis HitTest(Ray ray, Vector3 gizmoPos, float cameraDistance)
    {
        float size = cameraDistance * 0.18f;
        float threshold = size * 0.18f; // generous hit tolerance

        var bestAxis = GizmoAxis.None;
        float bestDist = float.MaxValue;

        if (Mode == GizmoMode.Rotate)
        {
            // For rotation, test proximity to ring circumference
            TestRingHit(ray, gizmoPos, Vector3.UnitX, size, threshold, GizmoAxis.X, ref bestAxis, ref bestDist);
            TestRingHit(ray, gizmoPos, Vector3.UnitY, size, threshold, GizmoAxis.Y, ref bestAxis, ref bestDist);
            TestRingHit(ray, gizmoPos, Vector3.UnitZ, size, threshold, GizmoAxis.Z, ref bestAxis, ref bestDist);
        }
        else
        {
            // Translate and scale both use axis lines
            TestAxisHit(ray, gizmoPos, Vector3.UnitX, size, threshold, GizmoAxis.X, ref bestAxis, ref bestDist);
            TestAxisHit(ray, gizmoPos, Vector3.UnitY, size, threshold, GizmoAxis.Y, ref bestAxis, ref bestDist);
            TestAxisHit(ray, gizmoPos, Vector3.UnitZ, size, threshold, GizmoAxis.Z, ref bestAxis, ref bestDist);
        }

        return bestAxis;
    }

    /// <summary>Begin dragging on an axis. Pass current transform state for the active mode.</summary>
    public void BeginDrag(GizmoAxis axis, Vector3 objectPos, Quaternion objectRot, Vector3 objectScale, Ray ray)
    {
        if (axis == GizmoAxis.None) return;

        DragAxis = axis;
        DragAngle = 0;
        _dragStartPos = objectPos;
        _dragStartRotation = objectRot;
        _dragStartScale = objectScale;
        _dragAxisDir = axis switch
        {
            GizmoAxis.X => Vector3.UnitX,
            GizmoAxis.Y => Vector3.UnitY,
            GizmoAxis.Z => Vector3.UnitZ,
            _ => Vector3.Zero,
        };
        _dragStartProjection = ProjectRayOntoAxis(ray, _dragStartPos, _dragAxisDir);

        // For rotation: store the initial direction from center to hit point on the ring plane
        if (Mode == GizmoMode.Rotate)
        {
            float denom = Vector3.Dot(_dragAxisDir, ray.Direction);
            if (MathF.Abs(denom) > 1e-6f)
            {
                float t = Vector3.Dot(_dragStartPos - ray.Origin, _dragAxisDir) / denom;
                var hitPoint = ray.Origin + ray.Direction * t;
                var toHit = hitPoint - _dragStartPos;
                float len = toHit.Length();
                _dragStartPlaneDir = len > 1e-6f ? toHit / len : GetPerpendicular(_dragAxisDir);
            }
            else
            {
                _dragStartPlaneDir = GetPerpendicular(_dragAxisDir);
            }
        }
    }

    /// <summary>Convenience overload for translate-only (backwards compat).</summary>
    public void BeginDrag(GizmoAxis axis, Vector3 objectPos, Ray ray) =>
        BeginDrag(axis, objectPos, Quaternion.Identity, Vector3.One, ray);

    /// <summary>
    /// Update the drag for Translate mode, returning the new position.
    /// </summary>
    public Vector3 UpdateTranslateDrag(Ray ray)
    {
        if (DragAxis == GizmoAxis.None) return _dragStartPos;
        float delta = ProjectRayOntoAxis(ray, _dragStartPos, _dragAxisDir) - _dragStartProjection;
        return _dragStartPos + _dragAxisDir * delta;
    }

    /// <summary>
    /// Update the drag for Rotate mode, returning the new rotation.
    /// Uses angular projection on the ring plane for natural circular dragging.
    /// </summary>
    public Quaternion UpdateRotateDrag(Ray ray)
    {
        if (DragAxis == GizmoAxis.None) return _dragStartRotation;

        // Intersect ray with the ring's plane
        float denom = Vector3.Dot(_dragAxisDir, ray.Direction);
        if (MathF.Abs(denom) < 1e-6f) return _dragStartRotation;

        float t = Vector3.Dot(_dragStartPos - ray.Origin, _dragAxisDir) / denom;
        var hitPoint = ray.Origin + ray.Direction * t;
        var toHit = hitPoint - _dragStartPos;
        float len = toHit.Length();
        if (len < 1e-6f) return _dragStartRotation;
        toHit /= len; // normalize

        // Calculate angle between start direction and current direction on the plane
        float cosAngle = Math.Clamp(Vector3.Dot(_dragStartPlaneDir, toHit), -1f, 1f);
        float sinAngle = Vector3.Dot(Vector3.Cross(_dragStartPlaneDir, toHit), _dragAxisDir);
        float angle = MathF.Atan2(sinAngle, cosAngle);

        DragAngle = angle;
        var rotation = Quaternion.CreateFromAxisAngle(_dragAxisDir, angle);
        return Quaternion.Normalize(rotation * _dragStartRotation);
    }

    /// <summary>
    /// Update the drag for Scale mode, returning the new scale.
    /// </summary>
    public Vector3 UpdateScaleDrag(Ray ray)
    {
        if (DragAxis == GizmoAxis.None) return _dragStartScale;
        float delta = ProjectRayOntoAxis(ray, _dragStartPos, _dragAxisDir) - _dragStartProjection;
        // Scale factor: drag right/up = grow, left/down = shrink
        float factor = 1f + delta;
        factor = MathF.Max(0.05f, factor); // prevent zero/negative scale

        var scale = _dragStartScale;
        return DragAxis switch
        {
            GizmoAxis.X => scale with { X = _dragStartScale.X * factor },
            GizmoAxis.Y => scale with { Y = _dragStartScale.Y * factor },
            GizmoAxis.Z => scale with { Z = _dragStartScale.Z * factor },
            _ => scale,
        };
    }

    /// <summary>End the drag.</summary>
    public void EndDrag()
    {
        DragAxis = GizmoAxis.None;
    }

    // --- Translate: arrow lines ---

    private void WriteTranslateAxis(ref int vi, Vector3 origin, Vector3 dir, float size, float arrowSize, GizmoAxis axis)
    {
        bool highlighted = HoveredAxis == axis || DragAxis == axis;
        var color = GetAxisColor(axis, highlighted);
        var tip = origin + dir * size;
        var perp1 = GetPerpendicular(dir);
        var perp2 = Vector3.Cross(dir, perp1);
        float thickness = size * 0.012f;

        // Draw 3 parallel lines for thickness (center + 2 offset)
        WriteVertex(ref vi, origin, color);
        WriteVertex(ref vi, tip, color);
        WriteVertex(ref vi, origin + perp1 * thickness, color);
        WriteVertex(ref vi, tip + perp1 * thickness, color);
        WriteVertex(ref vi, origin - perp1 * thickness, color);
        WriteVertex(ref vi, tip - perp1 * thickness, color);
        WriteVertex(ref vi, origin + perp2 * thickness, color);
        WriteVertex(ref vi, tip + perp2 * thickness, color);

        // Arrowhead — 4 diagonal lines for a pyramid shape
        var arrowBase = tip - dir * arrowSize;
        float arrowWidth = arrowSize * 0.5f;

        WriteVertex(ref vi, tip, color);
        WriteVertex(ref vi, arrowBase + perp1 * arrowWidth, color);
        WriteVertex(ref vi, tip, color);
        WriteVertex(ref vi, arrowBase - perp1 * arrowWidth, color);
        WriteVertex(ref vi, tip, color);
        WriteVertex(ref vi, arrowBase + perp2 * arrowWidth, color);
        WriteVertex(ref vi, tip, color);
        WriteVertex(ref vi, arrowBase - perp2 * arrowWidth, color);
        // Cross at arrowhead base for solidity
        WriteVertex(ref vi, arrowBase + perp1 * arrowWidth, color);
        WriteVertex(ref vi, arrowBase - perp1 * arrowWidth, color);
        WriteVertex(ref vi, arrowBase + perp2 * arrowWidth, color);
        WriteVertex(ref vi, arrowBase - perp2 * arrowWidth, color);
    }

    // --- Rotate: ring circles ---

    private void WriteRotateRing(ref int vi, Vector3 origin, Vector3 axis, float radius, GizmoAxis gizmoAxis)
    {
        bool highlighted = HoveredAxis == gizmoAxis || DragAxis == gizmoAxis;
        var color = GetAxisColor(gizmoAxis, highlighted);
        var perp1 = GetPerpendicular(axis);
        var perp2 = Vector3.Cross(axis, perp1);

        for (int i = 0; i < RingSegments; i++)
        {
            float a1 = i * MathF.Tau / RingSegments;
            float a2 = (i + 1) * MathF.Tau / RingSegments;
            var p1 = origin + (perp1 * MathF.Cos(a1) + perp2 * MathF.Sin(a1)) * radius;
            var p2 = origin + (perp1 * MathF.Cos(a2) + perp2 * MathF.Sin(a2)) * radius;
            WriteVertex(ref vi, p1, color);
            WriteVertex(ref vi, p2, color);
        }

        // Thicker appearance when highlighted: draw a second ring slightly offset
        if (highlighted)
        {
            float offset = radius * 0.02f;
            for (int i = 0; i < RingSegments; i++)
            {
                float a1 = i * MathF.Tau / RingSegments;
                float a2 = (i + 1) * MathF.Tau / RingSegments;
                var p1 = origin + (perp1 * MathF.Cos(a1) + perp2 * MathF.Sin(a1)) * (radius + offset);
                var p2 = origin + (perp1 * MathF.Cos(a2) + perp2 * MathF.Sin(a2)) * (radius + offset);
                WriteVertex(ref vi, p1, color);
                WriteVertex(ref vi, p2, color);
            }
        }
    }

    // --- Rotate: sweep arc showing drag angle ---

    private void WriteSweepArc(ref int vi, Vector3 origin, Vector3 axis, float radius, GizmoAxis gizmoAxis)
    {
        var color = GetAxisColor(gizmoAxis, true);
        color.W = 0.5f; // semi-transparent
        var perp1 = GetPerpendicular(axis);
        var perp2 = Vector3.Cross(axis, perp1);

        // Find start angle in the ring's local coordinate space
        float startAngle = MathF.Atan2(
            Vector3.Dot(_dragStartPlaneDir, perp2),
            Vector3.Dot(_dragStartPlaneDir, perp1));

        int arcSegments = Math.Min(RingSegments, Math.Max(4,
            (int)(MathF.Abs(DragAngle) / MathF.Tau * RingSegments)));
        float step = DragAngle / arcSegments;
        float innerRadius = radius * 0.82f;

        for (int i = 0; i < arcSegments; i++)
        {
            float a1 = startAngle + step * i;
            float a2 = startAngle + step * (i + 1);
            var p1 = origin + (perp1 * MathF.Cos(a1) + perp2 * MathF.Sin(a1)) * innerRadius;
            var p2 = origin + (perp1 * MathF.Cos(a2) + perp2 * MathF.Sin(a2)) * innerRadius;
            WriteVertex(ref vi, p1, color);
            WriteVertex(ref vi, p2, color);
        }
    }

    // --- Scale: axis lines with box endpoints ---

    private void WriteScaleAxis(ref int vi, Vector3 origin, Vector3 dir, float size, float boxSize, GizmoAxis axis)
    {
        bool highlighted = HoveredAxis == axis || DragAxis == axis;
        var color = GetAxisColor(axis, highlighted);
        var tip = origin + dir * size;
        var perp1 = GetPerpendicular(dir);
        var perp2 = Vector3.Cross(dir, perp1);
        float thickness = size * 0.012f;

        // Draw 3 parallel lines for thickness
        WriteVertex(ref vi, origin, color);
        WriteVertex(ref vi, tip, color);
        WriteVertex(ref vi, origin + perp1 * thickness, color);
        WriteVertex(ref vi, tip + perp1 * thickness, color);
        WriteVertex(ref vi, origin - perp1 * thickness, color);
        WriteVertex(ref vi, tip - perp1 * thickness, color);
        WriteVertex(ref vi, origin + perp2 * thickness, color);
        WriteVertex(ref vi, tip + perp2 * thickness, color);

        // Box at the tip — diamond shape
        float half = boxSize * 0.45f;
        WriteVertex(ref vi, tip - perp1 * half, color);
        WriteVertex(ref vi, tip + perp1 * half, color);
        WriteVertex(ref vi, tip - perp2 * half, color);
        WriteVertex(ref vi, tip + perp2 * half, color);
        // Box outline
        WriteVertex(ref vi, tip + perp1 * half, color);
        WriteVertex(ref vi, tip + perp2 * half, color);
        WriteVertex(ref vi, tip + perp2 * half, color);
        WriteVertex(ref vi, tip - perp1 * half, color);
        WriteVertex(ref vi, tip - perp1 * half, color);
        WriteVertex(ref vi, tip - perp2 * half, color);
        WriteVertex(ref vi, tip - perp2 * half, color);
        WriteVertex(ref vi, tip + perp1 * half, color);
    }

    private void WriteVertex(ref int vi, Vector3 pos, Vector4 color)
    {
        int i = vi * FloatsPerVertex;
        _vertexData[i] = pos.X; _vertexData[i + 1] = pos.Y; _vertexData[i + 2] = pos.Z;
        _vertexData[i + 3] = color.X; _vertexData[i + 4] = color.Y; _vertexData[i + 5] = color.Z; _vertexData[i + 6] = color.W;
        vi++;
    }

    private static Vector4 GetAxisColor(GizmoAxis axis, bool highlighted)
    {
        // Brighter, more saturated colors for better visibility
        return (axis, highlighted) switch
        {
            (GizmoAxis.X, true)  => new Vector4(1.0f, 0.25f, 0.25f, 1f),
            (GizmoAxis.X, false) => new Vector4(0.85f, 0.15f, 0.15f, 1f),
            (GizmoAxis.Y, true)  => new Vector4(0.3f, 1.0f, 0.3f, 1f),
            (GizmoAxis.Y, false) => new Vector4(0.15f, 0.85f, 0.15f, 1f),
            (GizmoAxis.Z, true)  => new Vector4(0.3f, 0.4f, 1.0f, 1f),
            (GizmoAxis.Z, false) => new Vector4(0.15f, 0.25f, 0.85f, 1f),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1f),
        };
    }

    private static Vector3 GetPerpendicular(Vector3 dir)
    {
        if (MathF.Abs(dir.Y) < 0.99f)
            return Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
        return Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitX));
    }

    private static void TestRingHit(Ray ray, Vector3 center, Vector3 normal, float radius, float threshold,
        GizmoAxis axis, ref GizmoAxis bestAxis, ref float bestDist)
    {
        // Intersect ray with the plane defined by center + normal
        float denom = Vector3.Dot(normal, ray.Direction);
        if (MathF.Abs(denom) < 1e-6f) return; // parallel to plane
        float t = Vector3.Dot(center - ray.Origin, normal) / denom;
        if (t < 0) return; // behind camera
        var hitPoint = ray.Origin + ray.Direction * t;
        // Distance from ring circumference
        float distFromCenter = Vector3.Distance(hitPoint, center);
        float distFromRing = MathF.Abs(distFromCenter - radius);
        if (distFromRing < threshold * 3f && distFromRing < bestDist)
        {
            bestDist = distFromRing;
            bestAxis = axis;
        }
    }

    private static void TestAxisHit(Ray ray, Vector3 origin, Vector3 axisDir, float size, float threshold,
        GizmoAxis axis, ref GizmoAxis bestAxis, ref float bestDist)
    {
        // Find closest points between ray and axis line
        var (_, _, dist, t) = ClosestPointsRayLine(ray, origin, origin + axisDir * size);

        // Must be within the axis length and close enough
        if (t >= -0.05f && t <= 1.05f && dist < threshold && dist < bestDist)
        {
            bestDist = dist;
            bestAxis = axis;
        }
    }

    /// <summary>
    /// Project a ray onto an axis to find the scalar position along that axis.
    /// Used for drag delta calculation.
    /// </summary>
    private static float ProjectRayOntoAxis(Ray ray, Vector3 axisOrigin, Vector3 axisDir)
    {
        // Find the point on the axis line closest to the ray
        var (_, axisPoint, _, _) = ClosestPointsRayLine(ray, axisOrigin, axisOrigin + axisDir * 1000f);
        return Vector3.Dot(axisPoint - axisOrigin, axisDir);
    }

    /// <summary>
    /// Find the closest points between a ray and a line segment.
    /// Returns (pointOnRay, pointOnLine, distance, t) where t is 0-1 along the segment.
    /// </summary>
    private static (Vector3 rayPoint, Vector3 linePoint, float distance, float t) ClosestPointsRayLine(
        Ray ray, Vector3 lineStart, Vector3 lineEnd)
    {
        var d1 = ray.Direction;
        var d2 = lineEnd - lineStart;
        var r = ray.Origin - lineStart;

        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);

        float b = Vector3.Dot(d1, d2);
        float c = Vector3.Dot(d1, r);
        float denom = a * e - b * b;

        float s, t;
        if (MathF.Abs(denom) < 1e-8f)
        {
            s = 0;
            t = MathF.Abs(e) > 1e-8f ? f / e : 0;
        }
        else
        {
            s = (b * f - c * e) / denom;
            t = (a * f - b * c) / denom;
        }

        s = MathF.Max(0, s); // ray can't go backwards
        t = MathF.Max(0, MathF.Min(1, t)); // clamp to segment

        var p1 = ray.Origin + d1 * s;
        var p2 = lineStart + d2 * t;
        return (p1, p2, Vector3.Distance(p1, p2), t);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _vertexBuffer.DisposeAsync();
    }

    // Same shader as EditorGrid — position + color, no depth test
    private const string GizmoShaderSource = @"
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
