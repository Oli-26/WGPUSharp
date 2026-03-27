using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// Spatial transform with position, rotation, and scale.
/// Caches the local and world model matrices, recomputing only when dirty.
/// </summary>
public sealed class Transform
{
    private Vector3 _position;
    private Quaternion _rotation = Quaternion.Identity;
    private Vector3 _scale = Vector3.One;
    private Matrix4x4 _localMatrix = Matrix4x4.Identity;
    private Matrix4x4 _worldMatrix = Matrix4x4.Identity;
    private bool _localDirty = true;
    private bool _worldDirty = true;

    public Vector3 Position
    {
        get => _position;
        set { _position = value; MarkDirty(); }
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set { _rotation = value; MarkDirty(); }
    }

    public Vector3 Scale
    {
        get => _scale;
        set { _scale = value; MarkDirty(); }
    }

    /// <summary>Euler angles in radians (pitch, yaw, roll). Convenience wrapper around Rotation.</summary>
    public Vector3 EulerAngles
    {
        get => QuaternionToEuler(_rotation);
        set => Rotation = Quaternion.CreateFromYawPitchRoll(value.Y, value.X, value.Z);
    }

    /// <summary>The local model matrix (scale * rotation * translation).</summary>
    public Matrix4x4 LocalMatrix
    {
        get
        {
            if (_localDirty) RecomputeLocal();
            return _localMatrix;
        }
    }

    /// <summary>
    /// The world matrix, incorporating the parent's world transform.
    /// Updated via <see cref="UpdateWorldMatrix(Matrix4x4)"/>.
    /// </summary>
    public Matrix4x4 WorldMatrix => _worldMatrix;

    /// <summary>Whether the local transform has changed since the last world matrix update.</summary>
    public bool IsDirty => _localDirty || _worldDirty;

    /// <summary>
    /// Recompute the world matrix from a parent world matrix.
    /// Returns true if the world matrix actually changed.
    /// </summary>
    public bool UpdateWorldMatrix(Matrix4x4 parentWorld)
    {
        if (!_localDirty && !_worldDirty) return false;

        if (_localDirty) RecomputeLocal();

        _worldMatrix = _localMatrix * parentWorld;
        _worldDirty = false;
        return true;
    }

    /// <summary>
    /// Recompute the world matrix as a root (no parent).
    /// </summary>
    public bool UpdateWorldMatrix()
    {
        if (!_localDirty && !_worldDirty) return false;

        if (_localDirty) RecomputeLocal();

        _worldMatrix = _localMatrix;
        _worldDirty = false;
        return true;
    }

    /// <summary>Mark this transform (and its world matrix) as needing recomputation.</summary>
    public void MarkDirty()
    {
        _localDirty = true;
        _worldDirty = true;
    }

    /// <summary>
    /// Forward direction in world space (negative Z by convention).
    /// </summary>
    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, _rotation);

    /// <summary>Right direction in world space.</summary>
    public Vector3 Right => Vector3.Transform(Vector3.UnitX, _rotation);

    /// <summary>Up direction in world space.</summary>
    public Vector3 Up => Vector3.Transform(Vector3.UnitY, _rotation);

    private void RecomputeLocal()
    {
        _localMatrix = Matrix4x4.CreateScale(_scale)
                     * Matrix4x4.CreateFromQuaternion(_rotation)
                     * Matrix4x4.CreateTranslation(_position);
        _localDirty = false;
    }

    private static Vector3 QuaternionToEuler(Quaternion q)
    {
        // Extract pitch (X), yaw (Y), roll (Z)
        float sinP = 2f * (q.W * q.X - q.Z * q.Y);
        float pitch = MathF.Abs(sinP) >= 1f
            ? MathF.CopySign(MathF.PI / 2f, sinP)
            : MathF.Asin(sinP);

        float sinYCosP = 2f * (q.W * q.Y + q.X * q.Z);
        float cosYCosP = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        float yaw = MathF.Atan2(sinYCosP, cosYCosP);

        float sinRCosP = 2f * (q.W * q.Z + q.X * q.Y);
        float cosRCosP = 1f - 2f * (q.X * q.X + q.Z * q.Z);
        float roll = MathF.Atan2(sinRCosP, cosRCosP);

        return new Vector3(pitch, yaw, roll);
    }
}
