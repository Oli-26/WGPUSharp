using System.Numerics;

namespace WgpuSharp.Mesh;

/// <summary>
/// Fast mesh simplification via vertex clustering.
/// Divides space into a uniform grid and merges vertices in the same cell.
/// Produces lower-detail meshes suitable for LOD at distance.
/// </summary>
public static class MeshSimplifier
{
    /// <summary>
    /// Simplify a mesh by clustering vertices into a grid.
    /// </summary>
    /// <param name="mesh">Input mesh.</param>
    /// <param name="cellSize">Grid cell size. Larger = more aggressive simplification.
    /// Typical values: 0.05 (subtle), 0.1 (medium), 0.2 (aggressive), 0.5 (very low poly).</param>
    /// <returns>A simplified mesh. Returns the original if simplification produces too few triangles.</returns>
    public static Mesh Simplify(Mesh mesh, float cellSize)
    {
        if (mesh.VertexCount < 12 || cellSize <= 0)
            return mesh;

        // Map each vertex to a grid cell, accumulate positions/normals/UVs per cell
        var cellMap = new Dictionary<long, CellData>();

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var pos = mesh.Positions[i];
            long key = CellKey(pos, cellSize);

            if (!cellMap.TryGetValue(key, out var cell))
            {
                cell = new CellData();
                cellMap[key] = cell;
            }

            cell.PositionSum += pos;
            if (mesh.HasNormals) cell.NormalSum += mesh.Normals[i];
            if (mesh.HasTexCoords) cell.TexCoordSum += mesh.TexCoords[i];
            cell.Count++;
        }

        // Assign each cell a new vertex index
        var cellIndices = new Dictionary<long, int>();
        var newPositions = new List<Vector3>();
        var newNormals = mesh.HasNormals ? new List<Vector3>() : null;
        var newTexCoords = mesh.HasTexCoords ? new List<Vector2>() : null;

        foreach (var (key, cell) in cellMap)
        {
            cellIndices[key] = newPositions.Count;
            float inv = 1f / cell.Count;
            newPositions.Add(cell.PositionSum * inv);
            var avgNormal = cell.NormalSum * inv;
            newNormals?.Add(avgNormal.LengthSquared() > 1e-8f ? Vector3.Normalize(avgNormal) : Vector3.UnitY);
            newTexCoords?.Add(cell.TexCoordSum * inv);
        }

        // Re-index triangles, skip degenerate ones
        var newIndices = new List<uint>();

        if (mesh.IndexCount > 0)
        {
            for (int i = 0; i < mesh.IndexCount; i += 3)
            {
                uint i0 = mesh.Indices[i], i1 = mesh.Indices[i + 1], i2 = mesh.Indices[i + 2];
                int a = cellIndices[CellKey(mesh.Positions[i0], cellSize)];
                int b = cellIndices[CellKey(mesh.Positions[i1], cellSize)];
                int c = cellIndices[CellKey(mesh.Positions[i2], cellSize)];

                // Skip degenerate triangles (two or more vertices merged to same cell)
                if (a == b || b == c || a == c) continue;

                newIndices.Add((uint)a);
                newIndices.Add((uint)b);
                newIndices.Add((uint)c);
            }
        }
        else
        {
            // Non-indexed mesh — treat every 3 vertices as a triangle
            for (int i = 0; i < mesh.VertexCount; i += 3)
            {
                if (i + 2 >= mesh.VertexCount) break;
                int a = cellIndices[CellKey(mesh.Positions[i], cellSize)];
                int b = cellIndices[CellKey(mesh.Positions[i + 1], cellSize)];
                int c = cellIndices[CellKey(mesh.Positions[i + 2], cellSize)];

                if (a == b || b == c || a == c) continue;

                newIndices.Add((uint)a);
                newIndices.Add((uint)b);
                newIndices.Add((uint)c);
            }
        }

        // If we didn't simplify enough or removed too much, return original
        if (newIndices.Count < 12 || newIndices.Count >= mesh.IndexCount)
            return mesh;

        var result = new Mesh
        {
            Positions = newPositions.ToArray(),
            Normals = newNormals?.Count == newPositions.Count ? newNormals.ToArray() : [],
            TexCoords = newTexCoords?.Count == newPositions.Count ? newTexCoords.ToArray() : [],
            Indices = newIndices.ToArray(),
            Material = mesh.Material,
        };

        // Ensure LOD mesh has normals if the source had normals — the renderer pipeline
        // expects a fixed vertex layout (pos + normal). Missing normals = garbled rendering.
        if (mesh.HasNormals && !result.HasNormals && result.IndexCount >= 3)
            result = result.ComputeFlatNormals();

        return result;
    }

    /// <summary>
    /// Generate LOD meshes at multiple detail levels.
    /// Returns [medium, low] detail meshes.
    /// </summary>
    public static Mesh[] GenerateLODs(Mesh mesh)
    {
        // Compute mesh bounding box to determine appropriate cell sizes
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var p in mesh.Positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        float extent = (max - min).Length();
        if (extent < 0.001f) extent = 1f;

        // Cell sizes relative to mesh extent
        float mediumCell = extent * 0.03f;  // ~50-70% triangle reduction
        float lowCell = extent * 0.06f;     // ~70-85% triangle reduction

        return
        [
            Simplify(mesh, mediumCell),
            Simplify(mesh, lowCell),
        ];
    }

    private static long CellKey(Vector3 pos, float cellSize)
    {
        int x = (int)MathF.Floor(pos.X / cellSize);
        int y = (int)MathF.Floor(pos.Y / cellSize);
        int z = (int)MathF.Floor(pos.Z / cellSize);
        // Pack into long (21 bits per axis, supports -1M to +1M range)
        return ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
    }

    private sealed class CellData
    {
        public Vector3 PositionSum;
        public Vector3 NormalSum;
        public Vector2 TexCoordSum;
        public int Count;
    }
}
