using System.Numerics;
using WgpuSharp.Core;

namespace WgpuSharp.Scene;

/// <summary>
/// Editor-style orbit camera. Rotates around a target point, supports zoom and pan.
/// </summary>
public sealed class OrbitCamera
{
    private float _yaw;
    private float _pitch = 0.4f;
    private float _distance = 8f;
    private Vector3 _target;
    private float _fovRadians = MathF.PI / 3f;
    private float _nearPlane = 0.1f;
    private float _farPlane = 500f;

    /// <summary>Horizontal angle in radians.</summary>
    public float Yaw { get => _yaw; set => _yaw = value; }

    /// <summary>Vertical angle in radians. Clamped to avoid gimbal lock.</summary>
    public float Pitch
    {
        get => _pitch;
        set => _pitch = MathF.Max(-MathF.PI / 2f + 0.01f, MathF.Min(MathF.PI / 2f - 0.01f, value));
    }

    /// <summary>Distance from the target point.</summary>
    public float Distance
    {
        get => _distance;
        set => _distance = MathF.Max(0.5f, MathF.Min(200f, value));
    }

    /// <summary>The point the camera orbits around.</summary>
    public Vector3 Target { get => _target; set => _target = value; }

    /// <summary>Field of view in radians.</summary>
    public float FovRadians { get => _fovRadians; set => _fovRadians = value; }

    /// <summary>Near clipping plane.</summary>
    public float NearPlane { get => _nearPlane; set => _nearPlane = value; }

    /// <summary>Far clipping plane.</summary>
    public float FarPlane { get => _farPlane; set => _farPlane = value; }

    /// <summary>Computed eye position based on yaw, pitch, and distance.</summary>
    public Vector3 Position
    {
        get
        {
            float cp = MathF.Cos(_pitch);
            return _target + new Vector3(
                MathF.Sin(_yaw) * cp * _distance,
                MathF.Sin(_pitch) * _distance,
                MathF.Cos(_yaw) * cp * _distance
            );
        }
    }

    /// <summary>View matrix (world → camera).</summary>
    public Matrix4x4 ViewMatrix => Matrix4x4.CreateLookAt(Position, _target, Vector3.UnitY);

    /// <summary>Projection matrix.</summary>
    public Matrix4x4 ProjectionMatrix(float aspectRatio) =>
        Matrix4x4.CreatePerspectiveFieldOfView(_fovRadians, aspectRatio, _nearPlane, _farPlane);

    /// <summary>Combined view-projection matrix.</summary>
    public Matrix4x4 ViewProjectionMatrix(float aspectRatio) =>
        ViewMatrix * ProjectionMatrix(aspectRatio);

    /// <summary>Camera's right direction in world space.</summary>
    public Vector3 Right => Vector3.Normalize(new Vector3(MathF.Cos(_yaw), 0, -MathF.Sin(_yaw)));

    /// <summary>Camera's up direction for panning (perpendicular to view and right).</summary>
    public Vector3 PanUp
    {
        get
        {
            var forward = Vector3.Normalize(_target - Position);
            return Vector3.Normalize(Vector3.Cross(Right, forward));
        }
    }

    /// <summary>
    /// Process input for the orbit camera.
    /// Left drag: orbit. Middle drag / Shift+left drag: pan. Scroll: zoom.
    /// </summary>
    public void ProcessInput(InputState input, float dt)
    {
        bool leftDown = input.IsMouseButtonDown(0);
        bool middleDown = input.IsMouseButtonDown(1);
        bool shiftDown = input.IsKeyDown("ShiftLeft") || input.IsKeyDown("ShiftRight");

        // Orbit (left drag without shift)
        if (leftDown && !shiftDown && input.PointerLocked)
        {
            _yaw -= input.MouseDX * 0.005f;
            Pitch += input.MouseDY * 0.005f;
        }

        // Pan (middle drag or shift+left drag)
        if ((middleDown || (leftDown && shiftDown)) && input.PointerLocked)
        {
            float panScale = _distance * 0.002f;
            _target -= Right * input.MouseDX * panScale;
            _target += PanUp * input.MouseDY * panScale;
        }

        // Zoom (scroll)
        if (input.WheelDelta != 0)
        {
            Distance *= input.WheelDelta > 0 ? 1.11f : 0.9f;
        }

    }

    /// <summary>
    /// Smoothly focus the camera on a world-space position.
    /// Sets the orbit target and adjusts distance to frame the point.
    /// </summary>
    public void FocusOn(Vector3 worldPosition, float desiredDistance = 0)
    {
        _target = worldPosition;
        if (desiredDistance > 0)
            Distance = desiredDistance;
    }

    /// <summary>
    /// Write the view-projection matrix to a byte array (64 bytes, column-major).
    /// </summary>
    public void WriteViewProjection(float aspectRatio, byte[] dest, int offset = 0)
    {
        var vp = ViewProjectionMatrix(aspectRatio);
        float[] values =
        [
            vp.M11, vp.M12, vp.M13, vp.M14,
            vp.M21, vp.M22, vp.M23, vp.M24,
            vp.M31, vp.M32, vp.M33, vp.M34,
            vp.M41, vp.M42, vp.M43, vp.M44,
        ];
        Buffer.BlockCopy(values, 0, dest, offset, 64);
    }
}
