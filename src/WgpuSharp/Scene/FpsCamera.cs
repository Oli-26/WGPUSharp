using System.Numerics;
using WgpuSharp.Core;

namespace WgpuSharp.Scene;

/// <summary>
/// First-person camera with physics: gravity, AABB collision, ground detection, jumping.
/// Used for play mode — walking around the scene as a player character.
/// </summary>
public sealed class FpsCamera
{
    private float _yaw;
    private float _pitch;
    private float _fovRadians = MathF.PI / 3f;
    private float _nearPlane = 0.1f;
    private float _farPlane = 500f;
    private Vector3 _velocity;
    private bool _grounded;

    /// <summary>Eye height above the player's feet.</summary>
    public const float EyeHeight = 1.6f;
    /// <summary>Player collision half-extents (width/2, height/2, depth/2).</summary>
    public static readonly Vector3 PlayerHalfExtents = new(0.3f, 0.9f, 0.3f);
    private const float GroundY = 0f; // Global ground plane at Y=0

    /// <summary>Camera position in world space (eye position).</summary>
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
    public float MoveSpeed { get; set; } = 6f;

    /// <summary>Mouse sensitivity multiplier.</summary>
    public float LookSensitivity { get; set; } = 0.003f;

    /// <summary>Gravity strength in units per second squared.</summary>
    public float GravityStrength { get; set; } = 15f;

    /// <summary>Vertical velocity applied when jumping.</summary>
    public float JumpVelocity { get; set; } = 6f;

    /// <summary>Whether the player is standing on something.</summary>
    public bool IsGrounded => _grounded;

    /// <summary>Current velocity vector. Can be set externally for game effects (springboards, knockback, etc.).</summary>
    public Vector3 Velocity { get => _velocity; set => _velocity = value; }

    /// <summary>Forward direction based on yaw and pitch.</summary>
    public Vector3 Forward => new(
        -MathF.Sin(_yaw) * MathF.Cos(_pitch),
        MathF.Sin(_pitch),
        -MathF.Cos(_yaw) * MathF.Cos(_pitch));

    /// <summary>Horizontal forward (for walking — no pitch component).</summary>
    public Vector3 HorizontalForward => Vector3.Normalize(new Vector3(-MathF.Sin(_yaw), 0, -MathF.Cos(_yaw)));

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

    /// <summary>
    /// Process input and physics for one frame.
    /// </summary>
    /// <param name="input">Current input state.</param>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    /// <param name="scene">Scene for collision testing (can be null for no collision).</param>
    public void ProcessInput(InputState input, float deltaTime, Scene? scene = null)
    {
        // Mouse look
        if (input.PointerLocked)
        {
            _yaw -= input.MouseDX * LookSensitivity;
            Pitch -= input.MouseDY * LookSensitivity;
        }

        // Scroll to change speed
        if (input.WheelDelta != 0)
        {
            MoveSpeed *= input.WheelDelta > 0 ? 0.85f : 1.18f;
            MoveSpeed = MathF.Max(1f, MathF.Min(50f, MoveSpeed));
        }

        // Horizontal movement intent
        var moveDir = Vector3.Zero;
        var hForward = HorizontalForward;
        var right = Right;

        if (input.IsKeyDown("KeyW")) moveDir += hForward;
        if (input.IsKeyDown("KeyS")) moveDir -= hForward;
        if (input.IsKeyDown("KeyA")) moveDir -= right;
        if (input.IsKeyDown("KeyD")) moveDir += right;

        if (moveDir.LengthSquared() > 0.001f)
            moveDir = Vector3.Normalize(moveDir);

        // Apply horizontal velocity
        _velocity.X = moveDir.X * MoveSpeed;
        _velocity.Z = moveDir.Z * MoveSpeed;

        // Jump
        if (_grounded && input.IsKeyDown("Space"))
            _velocity.Y = JumpVelocity;

        // Gravity
        if (!_grounded)
            _velocity.Y -= GravityStrength * deltaTime;

        // Compute feet position (position is eye, feet are below)
        var feetPos = Position - new Vector3(0, EyeHeight, 0);
        var newFeetPos = feetPos + _velocity * deltaTime;

        // Collision resolution
        if (scene is not null)
            newFeetPos = ResolveCollisions(feetPos, newFeetPos, scene);

        // Ground plane collision (always present at Y=0)
        if (newFeetPos.Y < GroundY)
        {
            newFeetPos.Y = GroundY;
            _velocity.Y = 0;
            _grounded = true;
        }
        else
        {
            // Check if grounded on scene objects
            _grounded = IsOnGround(newFeetPos, scene);
        }

        // Respawn if fallen into void
        if (newFeetPos.Y < -10f)
        {
            newFeetPos = new Vector3(0, 0, 0);
            _velocity = Vector3.Zero;
            _grounded = false;
        }

        Position = newFeetPos + new Vector3(0, EyeHeight, 0);
    }

    /// <summary>
    /// Resolve collisions by testing the player AABB against all scene object AABBs.
    /// Uses sweep-and-resolve per axis.
    /// </summary>
    private Vector3 ResolveCollisions(Vector3 oldFeet, Vector3 newFeet, Scene scene)
    {
        const float pw = 0.3f; // player half-width
        const float ph = 1.8f; // player height

        var result = newFeet;

        // Two-pass collision: vertical first (landing/ceiling), then horizontal (walls).
        // This prevents horizontal pushout from breaking vertical ground detection.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var node in scene.VisibleMeshNodesCached)
            {
                if (node.IsLight || !node.Solid) continue;

                var obj = ComputeWorldAABB(node.Transform.WorldMatrix);
                var pMin = new Vector3(result.X - pw, result.Y, result.Z - pw);
                var pMax = new Vector3(result.X + pw, result.Y + ph, result.Z + pw);

                if (!AABBOverlap(pMin, pMax, obj.Min, obj.Max)) continue;

                float ox = MathF.Min(pMax.X - obj.Min.X, obj.Max.X - pMin.X);
                float oy = MathF.Min(pMax.Y - obj.Min.Y, obj.Max.Y - pMin.Y);
                float oz = MathF.Min(pMax.Z - obj.Min.Z, obj.Max.Z - pMin.Z);
                if (ox <= 0 || oy <= 0 || oz <= 0) continue;

                if (pass == 0)
                {
                    // Pass 1: only resolve vertical overlaps
                    if (oy >= ox || oy >= oz) continue;
                    if (result.Y + ph * 0.5f < (obj.Min.Y + obj.Max.Y) * 0.5f)
                    { result.Y = obj.Min.Y - ph; if (_velocity.Y > 0) _velocity.Y = 0; }
                    else
                    { result.Y = obj.Max.Y; if (_velocity.Y < 0) _velocity.Y = 0; _grounded = true; }
                }
                else
                {
                    // Pass 2: only resolve horizontal overlaps
                    if (oy < ox && oy < oz) continue;
                    if (ox < oz)
                    {
                        if (result.X < (obj.Min.X + obj.Max.X) * 0.5f) result.X = obj.Min.X - pw;
                        else result.X = obj.Max.X + pw;
                        _velocity.X = 0;
                    }
                    else
                    {
                        if (result.Z < (obj.Min.Z + obj.Max.Z) * 0.5f) result.Z = obj.Min.Z - pw;
                        else result.Z = obj.Max.Z + pw;
                        _velocity.Z = 0;
                    }
                }
            }
        }

        return result;
    }

    private bool IsOnGround(Vector3 feetPos, Scene? scene)
    {
        if (feetPos.Y <= GroundY + 0.01f) return true;
        if (scene is null) return false;

        float pw = PlayerHalfExtents.X;
        // Check slightly below feet
        var checkMin = new Vector3(feetPos.X - pw, feetPos.Y - 0.05f, feetPos.Z - pw);
        var checkMax = new Vector3(feetPos.X + pw, feetPos.Y + 0.01f, feetPos.Z + pw);

        foreach (var node in scene.VisibleMeshNodesCached)
        {
            if (node.IsLight) continue;
            if (!node.Solid) continue;
            var objAABB = ComputeWorldAABB(node.Transform.WorldMatrix);
            if (AABBOverlap(checkMin, checkMax, objAABB.Min, objAABB.Max))
                return true;
        }
        return false;
    }

    private static bool AABBOverlap(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax) =>
        aMin.X < bMax.X && aMax.X > bMin.X &&
        aMin.Y < bMax.Y && aMax.Y > bMin.Y &&
        aMin.Z < bMax.Z && aMax.Z > bMin.Z;

    /// <summary>Compute the world-space AABB from a node's world transform applied to a unit cube.</summary>
    public static AABB ComputeWorldAABB(Matrix4x4 worldMatrix)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? -0.5f : 0.5f,
                (i & 2) == 0 ? -0.5f : 0.5f,
                (i & 4) == 0 ? -0.5f : 0.5f);
            var world = Vector3.Transform(corner, worldMatrix);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }
        return new AABB(min, max);
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

    /// <summary>Initialize from an orbit camera position (for smooth transition to play mode).</summary>
    public void InitFromOrbitCamera(OrbitCamera orbit)
    {
        Position = orbit.Target + new Vector3(0, EyeHeight, 0);
        _velocity = Vector3.Zero;
        _grounded = false;

        var lookDir = orbit.Target - orbit.Position;
        if (lookDir.LengthSquared() > 0.001f)
        {
            lookDir = Vector3.Normalize(lookDir);
            _yaw = MathF.Atan2(lookDir.X, -lookDir.Z);
            Pitch = MathF.Asin(MathF.Max(-1f, MathF.Min(1f, lookDir.Y)));
        }
    }
}
