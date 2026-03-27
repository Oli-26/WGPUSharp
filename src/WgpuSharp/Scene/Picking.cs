using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// A ray defined by an origin and a direction.
/// </summary>
public readonly struct Ray
{
    public readonly Vector3 Origin;
    public readonly Vector3 Direction;

    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = Vector3.Normalize(direction);
    }

    /// <summary>Get a point along the ray at distance t.</summary>
    public Vector3 GetPoint(float t) => Origin + Direction * t;
}

/// <summary>
/// Axis-aligned bounding box.
/// </summary>
public readonly struct AABB
{
    public readonly Vector3 Min;
    public readonly Vector3 Max;

    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Max - Min;

    /// <summary>
    /// Unit AABB centered at origin (-0.5 to 0.5 on each axis).
    /// Matches the default size of all primitives in <see cref="Primitives"/>.
    /// </summary>
    public static AABB Unit => new(new Vector3(-0.5f), new Vector3(0.5f));
}

/// <summary>
/// Utilities for picking objects in the scene via raycasting.
/// </summary>
public static class Picking
{
    /// <summary>
    /// Create a ray from the camera through a screen pixel.
    /// </summary>
    /// <param name="mouseX">Mouse X in canvas pixels (0 = left edge).</param>
    /// <param name="mouseY">Mouse Y in canvas pixels (0 = top edge).</param>
    /// <param name="canvasWidth">Canvas width in pixels.</param>
    /// <param name="canvasHeight">Canvas height in pixels.</param>
    /// <param name="viewMatrix">Camera view matrix.</param>
    /// <param name="projectionMatrix">Camera projection matrix.</param>
    public static Ray ScreenPointToRay(float mouseX, float mouseY,
        int canvasWidth, int canvasHeight,
        Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
    {
        // Convert pixel coordinates to NDC (-1 to 1)
        float ndcX = (2f * mouseX / canvasWidth) - 1f;
        float ndcY = 1f - (2f * mouseY / canvasHeight); // Y is flipped

        // Invert view-projection to get world-space ray
        var viewProj = viewMatrix * projectionMatrix;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
            return new Ray(Vector3.Zero, -Vector3.UnitZ);

        // Unproject near and far points
        var nearPoint = Unproject(new Vector3(ndcX, ndcY, 0f), invViewProj);
        var farPoint = Unproject(new Vector3(ndcX, ndcY, 1f), invViewProj);

        var direction = farPoint - nearPoint;
        if (direction.LengthSquared() < 1e-10f)
            return new Ray(nearPoint, -Vector3.UnitZ);

        return new Ray(nearPoint, direction);
    }

    /// <summary>
    /// Select all visible mesh nodes whose world-space center projects into a screen rectangle.
    /// </summary>
    public static List<SceneNode> BoxSelect(Scene scene, float x1, float y1, float x2, float y2,
        int canvasWidth, int canvasHeight, Matrix4x4 viewProjection)
    {
        var results = new List<SceneNode>();
        foreach (var node in scene.GetVisibleMeshNodes())
        {
            var worldPos = node.Transform.WorldMatrix.Translation;
            var clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProjection);
            if (clip.W <= 0) continue;
            float sx = (clip.X / clip.W * 0.5f + 0.5f) * canvasWidth;
            float sy = (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * canvasHeight;
            if (sx >= x1 && sx <= x2 && sy >= y1 && sy <= y2)
                results.Add(node);
        }
        return results;
    }

    /// <summary>
    /// Pick the nearest visible mesh node in the scene that the ray hits.
    /// Uses the node's world-transformed AABB for intersection.
    /// </summary>
    /// <returns>The hit node and distance, or null if nothing was hit.</returns>
    public static (SceneNode node, float distance)? PickNode(Ray ray, Scene scene)
    {
        SceneNode? closest = null;
        float closestDist = float.MaxValue;

        foreach (var node in scene.GetVisibleMeshNodes())
        {
            // Transform the ray into the node's local space for AABB test
            var worldMatrix = node.Transform.WorldMatrix;
            if (!Matrix4x4.Invert(worldMatrix, out var invWorld))
                continue;

            var localOrigin = Vector3.Transform(ray.Origin, invWorld);
            var localDir = Vector3.TransformNormal(ray.Direction, invWorld);
            if (localDir.LengthSquared() < 1e-10f) continue;
            localDir = Vector3.Normalize(localDir);

            var localRay = new Ray(localOrigin, localDir);

            if (RayIntersectsAABB(localRay, AABB.Unit, out float t) && t >= 0)
            {
                // Convert local hit distance to world distance
                var localHitPoint = localRay.GetPoint(t);
                var worldHitPoint = Vector3.Transform(localHitPoint, worldMatrix);
                float worldDist = Vector3.Distance(ray.Origin, worldHitPoint);

                if (worldDist < closestDist)
                {
                    closestDist = worldDist;
                    closest = node;
                }
            }
        }

        return closest is not null ? (closest, closestDist) : null;
    }

    /// <summary>
    /// Ray-AABB intersection test (slab method).
    /// </summary>
    public static bool RayIntersectsAABB(Ray ray, AABB box, out float tMin)
    {
        float tNear = float.MinValue;
        float tFar = float.MaxValue;

        for (int i = 0; i < 3; i++)
        {
            float origin = i switch { 0 => ray.Origin.X, 1 => ray.Origin.Y, _ => ray.Origin.Z };
            float dir = i switch { 0 => ray.Direction.X, 1 => ray.Direction.Y, _ => ray.Direction.Z };
            float bmin = i switch { 0 => box.Min.X, 1 => box.Min.Y, _ => box.Min.Z };
            float bmax = i switch { 0 => box.Max.X, 1 => box.Max.Y, _ => box.Max.Z };

            if (MathF.Abs(dir) < 1e-8f)
            {
                // Ray parallel to slab — miss if origin outside
                if (origin < bmin || origin > bmax)
                {
                    tMin = 0;
                    return false;
                }
            }
            else
            {
                float t1 = (bmin - origin) / dir;
                float t2 = (bmax - origin) / dir;
                if (t1 > t2) (t1, t2) = (t2, t1);

                tNear = MathF.Max(tNear, t1);
                tFar = MathF.Min(tFar, t2);

                if (tNear > tFar || tFar < 0)
                {
                    tMin = 0;
                    return false;
                }
            }
        }

        tMin = tNear >= 0 ? tNear : tFar;
        return true;
    }

    private static Vector3 Unproject(Vector3 ndc, Matrix4x4 invViewProj)
    {
        var clip = Vector4.Transform(new Vector4(ndc, 1f), invViewProj);
        if (MathF.Abs(clip.W) < 1e-10f) return new Vector3(clip.X, clip.Y, clip.Z);
        return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
    }
}
