using System.Numerics;
using System.Text;

namespace WgpuSharp.Mesh;

public static class StlLoader
{
    public static Mesh Load(Stream stream)
    {
        // Peek at start to determine if binary or ASCII
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();

        if (IsBinaryStl(data))
            return LoadBinary(data);

        return LoadAscii(Encoding.ASCII.GetString(data));
    }

    public static Mesh Load(byte[] data)
    {
        if (IsBinaryStl(data))
            return LoadBinary(data);

        return LoadAscii(Encoding.ASCII.GetString(data));
    }

    private static bool IsBinaryStl(byte[] data)
    {
        if (data.Length < 84) return false;

        // ASCII STL starts with "solid", but some binary files do too.
        // Check if the triangle count in the header matches the file size.
        uint triCount = BitConverter.ToUInt32(data, 80);
        long expectedSize = 84 + triCount * 50L;
        return data.Length == expectedSize;
    }

    private static Mesh LoadBinary(byte[] data)
    {
        uint triCount = BitConverter.ToUInt32(data, 80);
        var positions = new Vector3[triCount * 3];
        var normals = new Vector3[triCount * 3];
        var indices = new uint[triCount * 3];

        int offset = 84;
        for (uint t = 0; t < triCount; t++)
        {
            var normal = ReadVector3(data, offset); offset += 12;
            var v0 = ReadVector3(data, offset); offset += 12;
            var v1 = ReadVector3(data, offset); offset += 12;
            var v2 = ReadVector3(data, offset); offset += 12;
            offset += 2; // attribute byte count

            uint baseIdx = t * 3;
            positions[baseIdx] = v0;
            positions[baseIdx + 1] = v1;
            positions[baseIdx + 2] = v2;

            normals[baseIdx] = normal;
            normals[baseIdx + 1] = normal;
            normals[baseIdx + 2] = normal;

            indices[baseIdx] = baseIdx;
            indices[baseIdx + 1] = baseIdx + 1;
            indices[baseIdx + 2] = baseIdx + 2;
        }

        return new Mesh
        {
            Positions = positions,
            Normals = normals,
            Indices = indices,
        };
    }

    private static Mesh LoadAscii(string text)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<uint>();

        Vector3 currentNormal = Vector3.Zero;
        var faceVerts = new List<Vector3>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("facet normal", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    currentNormal = new Vector3(
                        float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture));
                }
                faceVerts.Clear();
            }
            else if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    faceVerts.Add(new Vector3(
                        float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
            else if (line.StartsWith("endfacet", StringComparison.OrdinalIgnoreCase))
            {
                if (faceVerts.Count >= 3)
                {
                    uint baseIdx = (uint)positions.Count;
                    for (int i = 0; i < faceVerts.Count; i++)
                    {
                        positions.Add(faceVerts[i]);
                        normals.Add(currentNormal);
                    }
                    for (int i = 1; i < faceVerts.Count - 1; i++)
                    {
                        indices.Add(baseIdx);
                        indices.Add(baseIdx + (uint)i);
                        indices.Add(baseIdx + (uint)i + 1);
                    }
                }
            }
        }

        return new Mesh
        {
            Positions = positions.ToArray(),
            Normals = normals.ToArray(),
            Indices = indices.ToArray(),
        };
    }

    private static Vector3 ReadVector3(byte[] data, int offset)
    {
        return new Vector3(
            BitConverter.ToSingle(data, offset),
            BitConverter.ToSingle(data, offset + 4),
            BitConverter.ToSingle(data, offset + 8));
    }
}
