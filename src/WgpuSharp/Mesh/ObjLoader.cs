using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

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

        // Use a struct key with custom comparer for faster hashing than tuple
        var vertexMap = new Dictionary<long, uint>();
        var outPositions = new List<Vector3>();
        var outNormals = new List<Vector3>();
        var outTexCoords = new List<Vector2>();
        var outIndices = new List<uint>();

        // Reusable face vertex buffer (avoids per-face allocation)
        var faceVerts = new uint[16]; // supports up to 16-gon faces

        Material? currentMaterial = null;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var span = line.AsSpan().Trim();
            if (span.Length == 0 || span[0] == '#') continue;

            // Fast prefix check without Split
            if (span.StartsWith("v "))
            {
                var rest = span[2..];
                positions.Add(new Vector3(
                    NextFloat(ref rest),
                    NextFloat(ref rest),
                    NextFloat(ref rest)));
            }
            else if (span.StartsWith("vn "))
            {
                var rest = span[3..];
                normals.Add(new Vector3(
                    NextFloat(ref rest),
                    NextFloat(ref rest),
                    NextFloat(ref rest)));
            }
            else if (span.StartsWith("vt "))
            {
                var rest = span[3..];
                texCoords.Add(new Vector2(
                    NextFloat(ref rest),
                    NextFloat(ref rest)));
            }
            else if (span.StartsWith("f "))
            {
                var rest = span[2..];
                int faceCount = 0;
                int posCount = positions.Count;
                int texCount = texCoords.Count;
                int normCount = normals.Count;

                while (rest.Length > 0)
                {
                    var token = NextToken(ref rest);
                    if (token.Length == 0) continue;

                    var idx = ParseFaceVertexSpan(token, posCount, texCount, normCount);
                    // Pack (p, t, n) into a single long for fast dictionary lookup
                    long key = ((long)(idx.p + 1) << 40) | ((long)(idx.t + 2) << 20) | (long)(idx.n + 2);

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

                    if (faceCount < faceVerts.Length)
                        faceVerts[faceCount] = vertexIndex;
                    faceCount++;
                }

                // Triangulate face (fan from first vertex)
                for (int i = 1; i < faceCount - 1; i++)
                {
                    outIndices.Add(faceVerts[0]);
                    outIndices.Add(faceVerts[i]);
                    outIndices.Add(faceVerts[i + 1]);
                }
            }
            else if (span.StartsWith("mtllib ") && mtlResolver is not null)
            {
                var mtlName = span[7..].Trim().ToString();
                var mtlText = mtlResolver(mtlName);
                if (mtlText is not null)
                    materials = MtlLoader.Load(mtlText);
            }
            else if (span.StartsWith("usemtl ") && materials is not null)
            {
                var matName = span[7..].Trim().ToString();
                materials.TryGetValue(matName, out currentMaterial);
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

    // --- Span-based zero-allocation parsers ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> NextToken(ref ReadOnlySpan<char> span)
    {
        // Skip leading whitespace
        int start = 0;
        while (start < span.Length && span[start] == ' ') start++;
        if (start >= span.Length) { span = ReadOnlySpan<char>.Empty; return ReadOnlySpan<char>.Empty; }

        // Find end of token
        int end = start;
        while (end < span.Length && span[end] != ' ') end++;

        var token = span[start..end];
        span = end < span.Length ? span[(end + 1)..] : ReadOnlySpan<char>.Empty;
        return token;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float NextFloat(ref ReadOnlySpan<char> span)
    {
        var token = NextToken(ref span);
        float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float val);
        return val;
    }

    private static (int p, int t, int n) ParseFaceVertexSpan(ReadOnlySpan<char> s, int posCount, int texCount, int normCount)
    {
        int slash1 = s.IndexOf('/');
        if (slash1 < 0)
        {
            int p = ParseIndexSpan(s, posCount);
            return (p, -1, -1);
        }

        int pIdx = ParseIndexSpan(s[..slash1], posCount);
        var rest = s[(slash1 + 1)..];

        int slash2 = rest.IndexOf('/');
        if (slash2 < 0)
        {
            int tIdx = rest.Length > 0 ? ParseIndexSpan(rest, texCount) : -1;
            return (pIdx, tIdx, -1);
        }

        int t = slash2 > 0 ? ParseIndexSpan(rest[..slash2], texCount) : -1;
        var nPart = rest[(slash2 + 1)..];
        int n = nPart.Length > 0 ? ParseIndexSpan(nPart, normCount) : -1;
        return (pIdx, t, n);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParseIndexSpan(ReadOnlySpan<char> s, int count)
    {
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx);
        return idx < 0 ? count + idx : idx - 1;
    }
}
