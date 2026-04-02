using System.Numerics;
using WgpuSharp.Core;

namespace WgpuSharp.Scene;

/// <summary>
/// Free-flying camera with no physics or collision.
/// WASD to move in look direction, Space/Shift for up/down, mouse to look, scroll to change speed.
/// </summary>
public sealed class FreeLookCamera
{
    private float _yaw;
    private float _pitch;
    private float _fovRadians = MathF.PI / 3f;
    private float _nearPlane = 0.1f;
    private float _farPlane = 500f;

    /// <summary>Camera position in world space.</summary>
    public Vector3 Position { get; set; }

    /// <summary>Horizontal look angle in radians.</summary>
    public float Yaw { get => _yaw; set => _yaw = value; }

    /// <summary>Vertical look angle in radians.</summary>
    public float Pitch
    {
        get => _pitch;
        set => _pitch = MathF.Max(-1.5f, MathF.Min(1.5f, value));
    }

    /// <summary>Movement speed in units per second.</summary>
    public float MoveSpeed { get; set; } = 8f;

    /// <summary>Mouse sensitivity multiplier.</summary>
    public float LookSensitivity { get; set; } = 0.003f;

    /// <summary>Forward direction based on yaw and pitch.</summary>
    public Vector3 Forward => new(
        -MathF.Sin(_yaw) * MathF.Cos(_pitch),
        MathF.Sin(_pitch),
        -MathF.Cos(_yaw) * MathF.Cos(_pitch));

    /// <summary>Right direction (horizontal only).</summary>
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));

    /// <summary>View matrix.</summary>
    public Matrix4x4 ViewMatrix => Matrix4x4.CreateLookAt(Position, Position + Forward, Vector3.UnitY);

    /// <summary>Projection matrix.</summary>
    public Matrix4x4 ProjectionMatrix(float aspectRatio) =>
        Matrix4x4.CreatePerspectiveFieldOfView(_fovRadians, aspectRatio, _nearPlane, _farPlane);

    /// <summary>Combined view-projection matrix.</summary>
    public Matrix4x4 ViewProjectionMatrix(float aspectRatio) =>
        ViewMatrix * ProjectionMatrix(aspectRatio);

    /// <summary>Process input for one frame. No gravity or collision.</summary>
    public void ProcessInput(InputState input, float deltaTime)
    {
        if (input.PointerLocked)
        {
            _yaw -= input.MouseDX * LookSensitivity;
            Pitch -= input.MouseDY * LookSensitivity;
        }

        if (input.WheelDelta != 0)
        {
            MoveSpeed *= input.WheelDelta > 0 ? 0.85f : 1.18f;
            MoveSpeed = MathF.Max(1f, MathF.Min(100f, MoveSpeed));
        }

        var moveDir = Vector3.Zero;
        var forward = Forward;
        var right = Right;

        if (input.IsKeyDown("KeyW")) moveDir += forward;
        if (input.IsKeyDown("KeyS")) moveDir -= forward;
        if (input.IsKeyDown("KeyA")) moveDir -= right;
        if (input.IsKeyDown("KeyD")) moveDir += right;
        if (input.IsKeyDown("Space")) moveDir += Vector3.UnitY;
        if (input.IsKeyDown("ShiftLeft") || input.IsKeyDown("ShiftRight")) moveDir -= Vector3.UnitY;

        if (moveDir.LengthSquared() > 0.001f)
            moveDir = Vector3.Normalize(moveDir);

        Position += moveDir * MoveSpeed * deltaTime;
    }

    /// <summary>Write the view-projection matrix to a byte array (64 bytes).</summary>
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

    /// <summary>Initialize from an orbit camera (starts at the orbit eye position).</summary>
    public void InitFromOrbitCamera(OrbitCamera orbit)
    {
        Position = orbit.Position;
        var lookDir = orbit.Target - orbit.Position;
        if (lookDir.LengthSquared() > 0.001f)
        {
            lookDir = Vector3.Normalize(lookDir);
            _yaw = MathF.Atan2(lookDir.X, -lookDir.Z);
            Pitch = MathF.Asin(MathF.Max(-1f, MathF.Min(1f, lookDir.Y)));
        }
    }

    /// <summary>Initialize from an FPS camera (preserves position and look direction).</summary>
    public void InitFromFpsCamera(FpsCamera fps)
    {
        Position = fps.Position;
        _yaw = fps.Yaw;
        Pitch = fps.Pitch;
    }
}
