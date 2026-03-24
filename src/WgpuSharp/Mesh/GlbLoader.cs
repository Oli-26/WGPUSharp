using System.Numerics;
using System.Text.Json;

namespace WgpuSharp.Mesh;

public static class GlbLoader
{
    private const uint GlbMagic = 0x46546C67; // "glTF"
    private const uint ChunkTypeJson = 0x4E4F534A; // "JSON"
    private const uint ChunkTypeBin = 0x004E4942; // "BIN\0"

    public static Mesh[] Load(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Load(ms.ToArray());
    }

    public static Mesh[] Load(byte[] data)
    {
        if (data.Length < 12)
            throw new InvalidDataException("GLB file too small.");

        uint magic = BitConverter.ToUInt32(data, 0);
        if (magic != GlbMagic)
            throw new InvalidDataException("Not a valid GLB file.");

        uint version = BitConverter.ToUInt32(data, 4);
        if (version != 2)
            throw new InvalidDataException($"Unsupported glTF version: {version}");

        byte[]? jsonBytes = null;
        byte[]? binBytes = null;
        int offset = 12;

        while (offset < data.Length)
        {
            uint chunkLength = BitConverter.ToUInt32(data, offset);
            uint chunkType = BitConverter.ToUInt32(data, offset + 4);
            offset += 8;

            if (chunkType == ChunkTypeJson)
            {
                jsonBytes = new byte[chunkLength];
                Array.Copy(data, offset, jsonBytes, 0, chunkLength);
            }
            else if (chunkType == ChunkTypeBin)
            {
                binBytes = new byte[chunkLength];
                Array.Copy(data, offset, binBytes, 0, chunkLength);
            }

            offset += (int)chunkLength;
        }

        if (jsonBytes is null)
            throw new InvalidDataException("GLB file has no JSON chunk.");

        var json = JsonDocument.Parse(jsonBytes);
        var root = json.RootElement;

        return ExtractMeshes(root, binBytes ?? []);
    }

    private static Mesh[] ExtractMeshes(JsonElement root, byte[] bin)
    {
        var bufferViews = root.GetProperty("bufferViews");
        var accessors = root.GetProperty("accessors");
        var meshes = root.GetProperty("meshes");

        // Pre-extract materials and images
        var materials = ExtractMaterials(root, bufferViews, bin);

        var result = new List<Mesh>();

        foreach (var mesh in meshes.EnumerateArray())
        {
            var primitives = mesh.GetProperty("primitives");
            foreach (var prim in primitives.EnumerateArray())
            {
                var attrs = prim.GetProperty("attributes");

                Vector3[] positions = [];
                if (attrs.TryGetProperty("POSITION", out var posAccessorIdx))
                    positions = ReadVec3Accessor(accessors[posAccessorIdx.GetInt32()], bufferViews, bin);

                Vector3[] normals = [];
                if (attrs.TryGetProperty("NORMAL", out var normAccessorIdx))
                    normals = ReadVec3Accessor(accessors[normAccessorIdx.GetInt32()], bufferViews, bin);

                Vector2[] texCoords = [];
                if (attrs.TryGetProperty("TEXCOORD_0", out var texAccessorIdx))
                    texCoords = ReadVec2Accessor(accessors[texAccessorIdx.GetInt32()], bufferViews, bin);

                uint[] indices = [];
                if (prim.TryGetProperty("indices", out var indicesAccessorIdx))
                    indices = ReadIndicesAccessor(accessors[indicesAccessorIdx.GetInt32()], bufferViews, bin);

                // Get material for this primitive
                Material material = Material.Default;
                if (prim.TryGetProperty("material", out var matIdx))
                {
                    int mi = matIdx.GetInt32();
                    if (mi >= 0 && mi < materials.Length)
                        material = materials[mi];
                }

                result.Add(new Mesh
                {
                    Positions = positions,
                    Normals = normals,
                    TexCoords = texCoords,
                    Indices = indices,
                    Material = material,
                });
            }
        }

        return result.ToArray();
    }

    private static Material[] ExtractMaterials(JsonElement root, JsonElement bufferViews, byte[] bin)
    {
        if (!root.TryGetProperty("materials", out var materialsEl))
            return [];

        // Pre-extract images (raw bytes)
        var imageBytes = ExtractImages(root, bufferViews, bin);

        // Map texture index → image index
        int[]? textureToImage = null;
        if (root.TryGetProperty("textures", out var texturesEl))
        {
            textureToImage = new int[texturesEl.GetArrayLength()];
            int ti = 0;
            foreach (var tex in texturesEl.EnumerateArray())
            {
                textureToImage[ti] = tex.TryGetProperty("source", out var src) ? src.GetInt32() : -1;
                ti++;
            }
        }

        var materials = new List<Material>();

        foreach (var mat in materialsEl.EnumerateArray())
        {
            string name = mat.TryGetProperty("name", out var n) ? n.GetString() ?? "unnamed" : "unnamed";
            var baseColor = Vector4.One;
            float metallic = 0f;
            float roughness = 1f;
            byte[]? baseColorTexData = null;

            if (mat.TryGetProperty("pbrMetallicRoughness", out var pbr))
            {
                if (pbr.TryGetProperty("baseColorFactor", out var bcf))
                {
                    var arr = bcf.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                    if (arr.Length >= 4)
                        baseColor = new Vector4(arr[0], arr[1], arr[2], arr[3]);
                }

                if (pbr.TryGetProperty("metallicFactor", out var mf))
                    metallic = mf.GetSingle();

                if (pbr.TryGetProperty("roughnessFactor", out var rf))
                    roughness = rf.GetSingle();

                // Base color texture
                if (pbr.TryGetProperty("baseColorTexture", out var bct))
                {
                    if (bct.TryGetProperty("index", out var texIdx))
                    {
                        int ti2 = texIdx.GetInt32();
                        if (textureToImage is not null && ti2 >= 0 && ti2 < textureToImage.Length)
                        {
                            int imgIdx = textureToImage[ti2];
                            if (imgIdx >= 0 && imgIdx < imageBytes.Length)
                                baseColorTexData = imageBytes[imgIdx];
                        }
                    }
                }
            }

            var emissive = Vector3.Zero;
            if (mat.TryGetProperty("emissiveFactor", out var ef))
            {
                var arr = ef.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                if (arr.Length >= 3)
                    emissive = new Vector3(arr[0], arr[1], arr[2]);
            }

            materials.Add(new Material
            {
                Name = name,
                BaseColor = baseColor,
                Metallic = metallic,
                Roughness = roughness,
                Emissive = emissive,
                BaseColorTextureData = baseColorTexData,
            });
        }

        return materials.ToArray();
    }

    private static byte[][] ExtractImages(JsonElement root, JsonElement bufferViews, byte[] bin)
    {
        if (!root.TryGetProperty("images", out var imagesEl))
            return [];

        var result = new List<byte[]>();

        foreach (var img in imagesEl.EnumerateArray())
        {
            if (img.TryGetProperty("bufferView", out var bvIdx))
            {
                var view = bufferViews[bvIdx.GetInt32()];
                int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
                int viewLength = view.GetProperty("byteLength").GetInt32();

                var bytes = new byte[viewLength];
                Array.Copy(bin, viewOffset, bytes, 0, viewLength);
                result.Add(bytes);
            }
            else
            {
                result.Add([]);
            }
        }

        return result.ToArray();
    }

    private static Vector3[] ReadVec3Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
    {
        int count = accessor.GetProperty("count").GetInt32();
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        int accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var view = bufferViews[viewIdx];
        int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
        int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 12;

        var result = new Vector3[count];
        int baseOffset = viewOffset + accessorOffset;

        for (int i = 0; i < count; i++)
        {
            int off = baseOffset + i * stride;
            result[i] = new Vector3(
                BitConverter.ToSingle(bin, off),
                BitConverter.ToSingle(bin, off + 4),
                BitConverter.ToSingle(bin, off + 8));
        }

        return result;
    }

    private static Vector2[] ReadVec2Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
    {
        int count = accessor.GetProperty("count").GetInt32();
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        int accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var view = bufferViews[viewIdx];
        int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
        int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 8;

        var result = new Vector2[count];
        int baseOffset = viewOffset + accessorOffset;

        for (int i = 0; i < count; i++)
        {
            int off = baseOffset + i * stride;
            result[i] = new Vector2(
                BitConverter.ToSingle(bin, off),
                BitConverter.ToSingle(bin, off + 4));
        }

        return result;
    }

    private static uint[] ReadIndicesAccessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
    {
        int count = accessor.GetProperty("count").GetInt32();
        int componentType = accessor.GetProperty("componentType").GetInt32();
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        int accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var view = bufferViews[viewIdx];
        int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;

        var result = new uint[count];
        int baseOffset = viewOffset + accessorOffset;

        for (int i = 0; i < count; i++)
        {
            result[i] = componentType switch
            {
                5121 => bin[baseOffset + i],
                5123 => BitConverter.ToUInt16(bin, baseOffset + i * 2),
                5125 => BitConverter.ToUInt32(bin, baseOffset + i * 4),
                _ => throw new InvalidDataException($"Unsupported index component type: {componentType}"),
            };
        }

        return result;
    }
}
