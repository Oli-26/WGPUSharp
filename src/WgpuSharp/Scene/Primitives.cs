using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// Generates common primitive meshes for use in the scene editor.
/// </summary>
public static class Primitives
{
    /// <summary>Unit cube centered at origin (positions + normals, indexed).</summary>
    public static Mesh.Mesh Cube()
    {
        var positions = new Vector3[24];
        var normals = new Vector3[24];
        var indices = new uint[36];

        // Face definitions: normal, then 4 corner offsets
        ReadOnlySpan<(Vector3 n, Vector3 a, Vector3 b, Vector3 c, Vector3 d)> faces =
        [
            (Vector3.UnitZ,  new(-0.5f,-0.5f, 0.5f), new( 0.5f,-0.5f, 0.5f), new( 0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)),
            (-Vector3.UnitZ, new( 0.5f,-0.5f,-0.5f), new(-0.5f,-0.5f,-0.5f), new(-0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f,-0.5f)),
            (Vector3.UnitY,  new(-0.5f, 0.5f, 0.5f), new( 0.5f, 0.5f, 0.5f), new( 0.5f, 0.5f,-0.5f), new(-0.5f, 0.5f,-0.5f)),
            (-Vector3.UnitY, new(-0.5f,-0.5f,-0.5f), new( 0.5f,-0.5f,-0.5f), new( 0.5f,-0.5f, 0.5f), new(-0.5f,-0.5f, 0.5f)),
            (Vector3.UnitX,  new( 0.5f,-0.5f, 0.5f), new( 0.5f,-0.5f,-0.5f), new( 0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f, 0.5f)),
            (-Vector3.UnitX, new(-0.5f,-0.5f,-0.5f), new(-0.5f,-0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f,-0.5f)),
        ];

        for (int f = 0; f < 6; f++)
        {
            var (n, a, b, c, d) = faces[f];
            int v = f * 4;
            positions[v] = a; positions[v + 1] = b; positions[v + 2] = c; positions[v + 3] = d;
            normals[v] = n; normals[v + 1] = n; normals[v + 2] = n; normals[v + 3] = n;

            int i = f * 6;
            indices[i] = (uint)v; indices[i + 1] = (uint)(v + 1); indices[i + 2] = (uint)(v + 2);
            indices[i + 3] = (uint)v; indices[i + 4] = (uint)(v + 2); indices[i + 5] = (uint)(v + 3);
        }

        return new Mesh.Mesh { Positions = positions, Normals = normals, Indices = indices };
    }

    /// <summary>UV sphere centered at origin with given segment counts.</summary>
    public static Mesh.Mesh Sphere(int rings = 16, int segments = 32)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<uint>();

        for (int r = 0; r <= rings; r++)
        {
            float phi = MathF.PI * r / rings;
            float y = MathF.Cos(phi);
            float sinPhi = MathF.Sin(phi);

            for (int s = 0; s <= segments; s++)
            {
                float theta = 2f * MathF.PI * s / segments;
                float x = sinPhi * MathF.Cos(theta);
                float z = sinPhi * MathF.Sin(theta);

                var n = new Vector3(x, y, z);
                positions.Add(n * 0.5f);
                normals.Add(n);
            }
        }

        int cols = segments + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                uint tl = (uint)(r * cols + s);
                uint tr = tl + 1;
                uint bl = (uint)((r + 1) * cols + s);
                uint br = bl + 1;

                indices.Add(tl); indices.Add(bl); indices.Add(tr);
                indices.Add(tr); indices.Add(bl); indices.Add(br);
            }
        }

        return new Mesh.Mesh
        {
            Positions = positions.ToArray(),
            Normals = normals.ToArray(),
            Indices = indices.ToArray(),
        };
    }

    /// <summary>Flat plane on the XZ plane, centered at origin.</summary>
    public static Mesh.Mesh Plane(int subdivisions = 1)
    {
        int verts = subdivisions + 1;
        var positions = new Vector3[verts * verts];
        var normals = new Vector3[verts * verts];
        var indices = new List<uint>();

        for (int z = 0; z < verts; z++)
        {
            for (int x = 0; x < verts; x++)
            {
                int idx = z * verts + x;
                float fx = (float)x / subdivisions - 0.5f;
                float fz = (float)z / subdivisions - 0.5f;
                positions[idx] = new Vector3(fx, 0, fz);
                normals[idx] = Vector3.UnitY;
            }
        }

        for (int z = 0; z < subdivisions; z++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                uint tl = (uint)(z * verts + x);
                uint tr = tl + 1;
                uint bl = (uint)((z + 1) * verts + x);
                uint br = bl + 1;

                indices.Add(tl); indices.Add(bl); indices.Add(tr);
                indices.Add(tr); indices.Add(bl); indices.Add(br);
            }
        }

        return new Mesh.Mesh
        {
            Positions = positions,
            Normals = normals,
            Indices = indices.ToArray(),
        };
    }

    /// <summary>Cylinder along the Y axis, centered at origin.</summary>
    public static Mesh.Mesh Cylinder(int segments = 32)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<uint>();

        // Side vertices
        for (int i = 0; i <= segments; i++)
        {
            float theta = 2f * MathF.PI * i / segments;
            float x = MathF.Cos(theta) * 0.5f;
            float z = MathF.Sin(theta) * 0.5f;
            var n = Vector3.Normalize(new Vector3(x, 0, z));

            positions.Add(new Vector3(x, -0.5f, z));
            normals.Add(n);
            positions.Add(new Vector3(x, 0.5f, z));
            normals.Add(n);
        }

        int sideCount = positions.Count;
        for (int i = 0; i < segments; i++)
        {
            uint bl = (uint)(i * 2);
            uint tl = bl + 1;
            uint br = bl + 2;
            uint tr = bl + 3;

            indices.Add(bl); indices.Add(br); indices.Add(tl);
            indices.Add(tl); indices.Add(br); indices.Add(tr);
        }

        // Top cap
        uint topCenter = (uint)positions.Count;
        positions.Add(new Vector3(0, 0.5f, 0));
        normals.Add(Vector3.UnitY);
        for (int i = 0; i <= segments; i++)
        {
            float theta = 2f * MathF.PI * i / segments;
            positions.Add(new Vector3(MathF.Cos(theta) * 0.5f, 0.5f, MathF.Sin(theta) * 0.5f));
            normals.Add(Vector3.UnitY);
        }
        for (int i = 0; i < segments; i++)
        {
            indices.Add(topCenter);
            indices.Add(topCenter + 1 + (uint)i);
            indices.Add(topCenter + 2 + (uint)i);
        }

        // Bottom cap
        uint botCenter = (uint)positions.Count;
        positions.Add(new Vector3(0, -0.5f, 0));
        normals.Add(-Vector3.UnitY);
        for (int i = 0; i <= segments; i++)
        {
            float theta = 2f * MathF.PI * i / segments;
            positions.Add(new Vector3(MathF.Cos(theta) * 0.5f, -0.5f, MathF.Sin(theta) * 0.5f));
            normals.Add(-Vector3.UnitY);
        }
        for (int i = 0; i < segments; i++)
        {
            indices.Add(botCenter);
            indices.Add(botCenter + 2 + (uint)i);
            indices.Add(botCenter + 1 + (uint)i);
        }

        return new Mesh.Mesh
        {
            Positions = positions.ToArray(),
            Normals = normals.ToArray(),
            Indices = indices.ToArray(),
        };
    }
}
