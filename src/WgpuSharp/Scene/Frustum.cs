using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// View frustum extracted from a view-projection matrix.
/// Used for culling objects outside the camera's visible area.
/// </summary>
public struct Frustum
{
    // 6 planes: left, right, bottom, top, near, far — inline to avoid array allocation
    private Vector4 _p0, _p1, _p2, _p3, _p4, _p5;

    public Frustum(Matrix4x4 vp)
    {
        Span<Vector4> planes = stackalloc Vector4[6];
        // Left
        planes[0] = new Vector4(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41);
        // Right
        planes[1] = new Vector4(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41);
        // Bottom
        planes[2] = new Vector4(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42);
        // Top
        planes[3] = new Vector4(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42);
        // Near
        planes[4] = new Vector4(vp.M13, vp.M23, vp.M33, vp.M43);
        // Far
        planes[5] = new Vector4(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43);

        // Normalize and store inline
        for (int i = 0; i < 6; i++)
        {
            float len = new Vector3(planes[i].X, planes[i].Y, planes[i].Z).Length();
            if (len > 1e-6f) planes[i] /= len;
        }
        _p0 = planes[0]; _p1 = planes[1]; _p2 = planes[2];
        _p3 = planes[3]; _p4 = planes[4]; _p5 = planes[5];
    }

    private readonly float TestPlane(Vector4 plane, Vector3 center) =>
        plane.X * center.X + plane.Y * center.Y + plane.Z * center.Z + plane.W;

    /// <summary>
    /// Test if a sphere (center + radius) is inside or intersecting the frustum.
    /// </summary>
    public readonly bool ContainsSphere(Vector3 center, float radius)
    {
        if (TestPlane(_p0, center) < -radius) return false;
        if (TestPlane(_p1, center) < -radius) return false;
        if (TestPlane(_p2, center) < -radius) return false;
        if (TestPlane(_p3, center) < -radius) return false;
        if (TestPlane(_p4, center) < -radius) return false;
        if (TestPlane(_p5, center) < -radius) return false;
        return true;
    }

    /// <summary>
    /// Test if an AABB is inside or intersecting the frustum.
    /// Uses the "p-vertex" approach for fast rejection.
    /// </summary>
    public readonly bool ContainsAABB(Vector3 min, Vector3 max)
    {
        if (TestAABBPlane(_p0, min, max) < 0) return false;
        if (TestAABBPlane(_p1, min, max) < 0) return false;
        if (TestAABBPlane(_p2, min, max) < 0) return false;
        if (TestAABBPlane(_p3, min, max) < 0) return false;
        if (TestAABBPlane(_p4, min, max) < 0) return false;
        if (TestAABBPlane(_p5, min, max) < 0) return false;
        return true;
    }

    private static float TestAABBPlane(Vector4 plane, Vector3 min, Vector3 max)
    {
        var p = new Vector3(
            plane.X >= 0 ? max.X : min.X,
            plane.Y >= 0 ? max.Y : min.Y,
            plane.Z >= 0 ? max.Z : min.Z);
        return plane.X * p.X + plane.Y * p.Y + plane.Z * p.Z + plane.W;
    }
}
