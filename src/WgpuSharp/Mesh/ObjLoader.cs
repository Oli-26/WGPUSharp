using System.Globalization;
using System.Numerics;

namespace WgpuSharp.Mesh;

public static class ObjLoader
{
    public static Mesh Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Load(reader, null);
    }

    public static Mesh Load(string objText, string? mtlText = null)
    {
        Dictionary<string, Material>? materials = null;
        if (mtlText is not null)
            materials = MtlLoader.Load(mtlText);

        using var reader = new StringReader(objText);
        return Load(reader, materials);
    }

    /// <summary>
    /// Load OBJ with a function to resolve mtllib references (returns MTL text).
    /// </summary>
    public static Mesh Load(Stream stream, Func<string, string?>? mtlResolver)
    {
        using var reader = new StreamReader(stream);
        return Load(reader, null, mtlResolver);
    }

    private static Mesh Load(TextReader reader, Dictionary<string, Material>? materials, Func<string, string?>? mtlResolver = null)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();

        var vertexMap = new Dictionary<(int p, int t, int n), uint>();
        var outPositions = new List<Vector3>();
        var outNormals = new List<Vector3>();
        var outTexCoords = new List<Vector2>();
        var outIndices = new List<uint>();

        Material? currentMaterial = null;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "mtllib" when parts.Length >= 2 && mtlResolver is not null:
                    var mtlText = mtlResolver(parts[1]);
                    if (mtlText is not null)
                        materials = MtlLoader.Load(mtlText);
                    break;

                case "usemtl" when parts.Length >= 2 && materials is not null:
                    materials.TryGetValue(parts[1], out currentMaterial);
                    break;

                case "v" when parts.Length >= 4:
                    positions.Add(new Vector3(
                        ParseFloat(parts[1]),
                        ParseFloat(parts[2]),
                        ParseFloat(parts[3])));
                    break;

                case "vn" when parts.Length >= 4:
                    normals.Add(new Vector3(
                        ParseFloat(parts[1]),
                        ParseFloat(parts[2]),
                        ParseFloat(parts[3])));
                    break;

                case "vt" when parts.Length >= 3:
                    texCoords.Add(new Vector2(
                        ParseFloat(parts[1]),
                        ParseFloat(parts[2])));
                    break;

                case "f":
                    var faceVerts = new List<uint>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var idx = ParseFaceVertex(parts[i], positions.Count, texCoords.Count, normals.Count);
                        var key = (idx.p, idx.t, idx.n);

                        if (!vertexMap.TryGetValue(key, out uint vertexIndex))
                        {
                            vertexIndex = (uint)outPositions.Count;
                            vertexMap[key] = vertexIndex;

                            outPositions.Add(positions[idx.p]);

                            if (idx.n >= 0 && idx.n < normals.Count)
                                outNormals.Add(normals[idx.n]);

                            if (idx.t >= 0 && idx.t < texCoords.Count)
                                outTexCoords.Add(texCoords[idx.t]);
                        }
                        faceVerts.Add(vertexIndex);
                    }

                    for (int i = 1; i < faceVerts.Count - 1; i++)
                    {
                        outIndices.Add(faceVerts[0]);
                        outIndices.Add(faceVerts[i]);
                        outIndices.Add(faceVerts[i + 1]);
                    }
                    break;
            }
        }

        return new Mesh
        {
            Positions = outPositions.ToArray(),
            Normals = outNormals.Count == outPositions.Count ? outNormals.ToArray() : [],
            TexCoords = outTexCoords.Count == outPositions.Count ? outTexCoords.ToArray() : [],
            Indices = outIndices.ToArray(),
            Material = currentMaterial ?? Material.Default,
        };
    }

    private static (int p, int t, int n) ParseFaceVertex(string s, int posCount, int texCount, int normCount)
    {
        var parts = s.Split('/');
        int p = ParseIndex(parts[0], posCount);
        int t = parts.Length > 1 && parts[1].Length > 0 ? ParseIndex(parts[1], texCount) : -1;
        int n = parts.Length > 2 && parts[2].Length > 0 ? ParseIndex(parts[2], normCount) : -1;
        return (p, t, n);
    }

    private static int ParseIndex(string s, int count)
    {
        int idx = int.Parse(s, CultureInfo.InvariantCulture);
        return idx < 0 ? count + idx : idx - 1;
    }

    private static float ParseFloat(string s)
        => float.Parse(s, CultureInfo.InvariantCulture);
}
