using System.Numerics;
using System.Text.Json;
using WgpuSharp.Scene;

namespace WgpuSharp.Mesh;

/// <summary>Result of loading a GLB with optional skin/animation data.</summary>
public sealed class GlbResult
{
    /// <summary>Static meshes (non-skinned primitives).</summary>
    public Mesh[] Meshes { get; init; } = [];
    /// <summary>Skinned meshes (one per skinned primitive), or empty.</summary>
    public SkinnedMesh[] SkinnedMeshes { get; init; } = [];
    /// <summary>Skeleton, or null if no skin.</summary>
    public Skeleton? Skeleton { get; init; }
    /// <summary>Animation clips, or empty.</summary>
    public AnimationClip[] Animations { get; init; } = [];
    /// <summary>Whether this GLB has skeletal animation data.</summary>
    public bool HasSkin => Skeleton is not null;
}

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

    // ── Skinned mesh loading ──────────────────────────────────────────

    /// <summary>
    /// Load a GLB file and extract skinned meshes, skeleton, and animations.
    /// Falls back gracefully if no skin data is present.
    /// </summary>
    public static GlbResult LoadWithSkin(byte[] data)
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
        var bin = binBytes ?? [];

        // Check if there's skin data
        if (!root.TryGetProperty("skins", out var skinsEl) || skinsEl.GetArrayLength() == 0)
        {
            // No skin — return static meshes only
            return new GlbResult { Meshes = ExtractMeshes(root, bin) };
        }

        var bufferViews = root.GetProperty("bufferViews");
        var accessors = root.GetProperty("accessors");
        var nodes = root.GetProperty("nodes");

        // Extract skeleton from first skin
        var skinEl = skinsEl[0];
        var skeleton = ExtractSkeleton(skinEl, nodes, accessors, bufferViews, bin);

        // Build joint node index → joint index map
        var jointNodeIndices = skinEl.GetProperty("joints");
        var nodeToJoint = new Dictionary<int, int>();
        for (int i = 0; i < jointNodeIndices.GetArrayLength(); i++)
            nodeToJoint[jointNodeIndices[i].GetInt32()] = i;

        // Find which mesh index is used by the skinned node
        int skinnedMeshIdx = -1;
        for (int ni = 0; ni < nodes.GetArrayLength(); ni++)
        {
            var node = nodes[ni];
            if (node.TryGetProperty("skin", out _) && node.TryGetProperty("mesh", out var meshIdx))
            {
                skinnedMeshIdx = meshIdx.GetInt32();
                break;
            }
        }

        // Extract skinned meshes
        var staticMeshes = new List<Mesh>();
        var skinnedMeshes = new List<SkinnedMesh>();
        var meshes = root.GetProperty("meshes");
        var materials = ExtractMaterials(root, bufferViews, bin);

        for (int mi = 0; mi < meshes.GetArrayLength(); mi++)
        {
            var mesh = meshes[mi];
            var primitives = mesh.GetProperty("primitives");
            foreach (var prim in primitives.EnumerateArray())
            {
                var attrs = prim.GetProperty("attributes");

                Vector3[] positions = [];
                if (attrs.TryGetProperty("POSITION", out var posIdx))
                    positions = ReadVec3Accessor(accessors[posIdx.GetInt32()], bufferViews, bin);

                Vector3[] normals = [];
                if (attrs.TryGetProperty("NORMAL", out var normIdx))
                    normals = ReadVec3Accessor(accessors[normIdx.GetInt32()], bufferViews, bin);

                uint[] indices = [];
                if (prim.TryGetProperty("indices", out var indIdx))
                    indices = ReadIndicesAccessor(accessors[indIdx.GetInt32()], bufferViews, bin);

                if (mi == skinnedMeshIdx
                    && attrs.TryGetProperty("JOINTS_0", out var jointsIdx)
                    && attrs.TryGetProperty("WEIGHTS_0", out var weightsIdx))
                {
                    // Compute flat normals if needed
                    if (normals.Length == 0 && indices.Length >= 3)
                    {
                        var tempMesh = new Mesh { Positions = positions, Indices = indices }.ComputeFlatNormals();
                        positions = tempMesh.Positions;
                        normals = tempMesh.Normals;
                        indices = tempMesh.Indices;
                        // Need to expand joints/weights to match the new vertices
                        var origJoints = ReadJointsAccessor(accessors[jointsIdx.GetInt32()], bufferViews, bin);
                        var origWeights = ReadVec4Accessor(accessors[weightsIdx.GetInt32()], bufferViews, bin);
                        var newJoints = new byte[positions.Length * 4];
                        var newWeights = new Vector4[positions.Length];
                        for (int i = 0; i < indices.Length; i++)
                        {
                            // After ComputeFlatNormals, index[i] == i, and original index was Indices[i] before expansion
                            // But ComputeFlatNormals already expanded vertices, so we use the original mesh's indices
                        }
                        // Actually, ComputeFlatNormals creates new vertices from the original indexed vertices.
                        // The original indices map to the original vertex data. We need to re-expand.
                        // Since ComputeFlatNormals remaps indices[t*3+k] -> newIndex = t*3+k, using origIndex = originalIndices[t*3+k]
                        // We need the original indices before ComputeFlatNormals was called.
                        // Simpler: just expand joints/weights the same way ComputeFlatNormals expands positions.
                        // Re-do with original data:
                        skinnedMeshes.Add(ExpandSkinnedMesh(
                            ReadVec3Accessor(accessors[attrs.GetProperty("POSITION").GetInt32()], bufferViews, bin),
                            ReadIndicesAccessor(accessors[prim.GetProperty("indices").GetInt32()], bufferViews, bin),
                            origJoints, origWeights));
                        continue;
                    }

                    var jointData = ReadJointsAccessor(accessors[jointsIdx.GetInt32()], bufferViews, bin);
                    var weightData = ReadVec4Accessor(accessors[weightsIdx.GetInt32()], bufferViews, bin);

                    skinnedMeshes.Add(new SkinnedMesh
                    {
                        Positions = positions,
                        Normals = normals,
                        Indices = indices,
                        JointIndices = jointData,
                        Weights = weightData,
                    });
                }
                else
                {
                    Material material = Material.Default;
                    if (prim.TryGetProperty("material", out var matIdx))
                    {
                        int mii = matIdx.GetInt32();
                        if (mii >= 0 && mii < materials.Length) material = materials[mii];
                    }
                    staticMeshes.Add(new Mesh
                    {
                        Positions = positions, Normals = normals, Indices = indices, Material = material,
                    });
                }
            }
        }

        // Extract animations
        var animations = ExtractAnimations(root, accessors, bufferViews, bin, nodeToJoint);

        return new GlbResult
        {
            Meshes = staticMeshes.ToArray(),
            SkinnedMeshes = skinnedMeshes.ToArray(),
            Skeleton = skeleton,
            Animations = animations,
        };
    }

    /// <summary>Expand a skinned mesh that needs flat normals (no normals in source data).</summary>
    private static SkinnedMesh ExpandSkinnedMesh(Vector3[] origPos, uint[] origIndices,
        byte[] origJoints, Vector4[] origWeights)
    {
        int triCount = origIndices.Length / 3;
        int vertCount = origIndices.Length;
        var positions = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var joints = new byte[vertCount * 4];
        var weights = new Vector4[vertCount];
        var indices = new uint[vertCount];

        for (int t = 0; t < triCount; t++)
        {
            uint i0 = origIndices[t * 3], i1 = origIndices[t * 3 + 1], i2 = origIndices[t * 3 + 2];
            var p0 = origPos[i0]; var p1 = origPos[i1]; var p2 = origPos[i2];
            var normal = Vector3.Normalize(Vector3.Cross(p1 - p0, p2 - p0));

            int b = t * 3;
            positions[b] = p0; positions[b + 1] = p1; positions[b + 2] = p2;
            normals[b] = normal; normals[b + 1] = normal; normals[b + 2] = normal;

            for (int k = 0; k < 3; k++)
            {
                int src = (int)origIndices[t * 3 + k];
                int dst = b + k;
                joints[dst * 4] = origJoints[src * 4];
                joints[dst * 4 + 1] = origJoints[src * 4 + 1];
                joints[dst * 4 + 2] = origJoints[src * 4 + 2];
                joints[dst * 4 + 3] = origJoints[src * 4 + 3];
                weights[dst] = origWeights[src];
                indices[dst] = (uint)dst;
            }
        }

        return new SkinnedMesh
        {
            Positions = positions, Normals = normals, Indices = indices,
            JointIndices = joints, Weights = weights,
        };
    }

    private static Skeleton ExtractSkeleton(JsonElement skinEl, JsonElement nodes,
        JsonElement accessors, JsonElement bufferViews, byte[] bin)
    {
        var jointNodeIndices = skinEl.GetProperty("joints");
        int jointCount = jointNodeIndices.GetArrayLength();

        // Map glTF node index → joint index
        var nodeToJoint = new Dictionary<int, int>();
        for (int i = 0; i < jointCount; i++)
            nodeToJoint[jointNodeIndices[i].GetInt32()] = i;

        // Read inverse bind matrices
        Matrix4x4[] ibms;
        if (skinEl.TryGetProperty("inverseBindMatrices", out var ibmIdx))
            ibms = ReadMat4Accessor(accessors[ibmIdx.GetInt32()], bufferViews, bin);
        else
        {
            ibms = new Matrix4x4[jointCount];
            Array.Fill(ibms, Matrix4x4.Identity);
        }

        // Build parent map from node children
        var parentMap = new int[jointCount];
        Array.Fill(parentMap, -1);

        for (int ni = 0; ni < nodes.GetArrayLength(); ni++)
        {
            if (!nodeToJoint.TryGetValue(ni, out int parentJoint)) continue;
            var node = nodes[ni];
            if (!node.TryGetProperty("children", out var children)) continue;
            foreach (var child in children.EnumerateArray())
            {
                int childNodeIdx = child.GetInt32();
                if (nodeToJoint.TryGetValue(childNodeIdx, out int childJoint))
                    parentMap[childJoint] = parentJoint;
            }
        }

        // Build joints with rest pose TRS
        var joints = new Joint[jointCount];
        for (int i = 0; i < jointCount; i++)
        {
            int nodeIdx = jointNodeIndices[i].GetInt32();
            var node = nodes[nodeIdx];

            var translation = Vector3.Zero;
            var rotation = Quaternion.Identity;
            var scale = Vector3.One;

            if (node.TryGetProperty("translation", out var tEl))
            {
                var arr = tEl.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                if (arr.Length >= 3) translation = new Vector3(arr[0], arr[1], arr[2]);
            }
            if (node.TryGetProperty("rotation", out var rEl))
            {
                var arr = rEl.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                if (arr.Length >= 4) rotation = new Quaternion(arr[0], arr[1], arr[2], arr[3]);
            }
            if (node.TryGetProperty("scale", out var sEl))
            {
                var arr = sEl.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                if (arr.Length >= 3) scale = new Vector3(arr[0], arr[1], arr[2]);
            }

            string name = node.TryGetProperty("name", out var n) ? n.GetString() ?? $"Joint{i}" : $"Joint{i}";

            joints[i] = new Joint
            {
                Index = i,
                Name = name,
                ParentIndex = parentMap[i],
                Translation = translation,
                Rotation = rotation,
                Scale = scale,
            };
        }

        // Topological sort (parents before children) — already guaranteed by glTF spec
        // but verify and fix if needed
        // Simple check: if any joint has a parent with higher index, we need to reorder
        bool needsSort = false;
        for (int i = 0; i < jointCount; i++)
        {
            if (joints[i].ParentIndex > i) { needsSort = true; break; }
        }

        if (needsSort)
        {
            // BFS from roots
            var sorted = new List<int>();
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            for (int i = 0; i < jointCount; i++)
                if (parentMap[i] < 0) queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int j = queue.Dequeue();
                if (!visited.Add(j)) continue;
                sorted.Add(j);
                for (int k = 0; k < jointCount; k++)
                    if (parentMap[k] == j) queue.Enqueue(k);
            }
            // Add any orphans
            for (int i = 0; i < jointCount; i++)
                if (!visited.Contains(i)) sorted.Add(i);

            // Remap
            var oldToNew = new int[jointCount];
            for (int i = 0; i < sorted.Count; i++)
                oldToNew[sorted[i]] = i;

            var newJoints = new Joint[jointCount];
            var newIbms = new Matrix4x4[jointCount];
            for (int i = 0; i < sorted.Count; i++)
            {
                int old = sorted[i];
                int newParent = parentMap[old] >= 0 ? oldToNew[parentMap[old]] : -1;
                newJoints[i] = new Joint
                {
                    Index = i,
                    Name = joints[old].Name,
                    ParentIndex = newParent,
                    Translation = joints[old].Translation,
                    Rotation = joints[old].Rotation,
                    Scale = joints[old].Scale,
                };
                newIbms[i] = ibms[old];
            }
            joints = newJoints;
            ibms = newIbms;
        }

        return new Skeleton { Joints = joints, InverseBindMatrices = ibms };
    }

    private static AnimationClip[] ExtractAnimations(JsonElement root, JsonElement accessors,
        JsonElement bufferViews, byte[] bin, Dictionary<int, int> nodeToJoint)
    {
        if (!root.TryGetProperty("animations", out var animsEl))
            return [];

        var clips = new List<AnimationClip>();
        foreach (var anim in animsEl.EnumerateArray())
        {
            string name = anim.TryGetProperty("name", out var n) ? n.GetString() ?? "Untitled" : "Untitled";
            var samplers = anim.GetProperty("samplers");
            var channels = anim.GetProperty("channels");

            var animChannels = new List<AnimationChannel>();
            float maxTime = 0;

            foreach (var ch in channels.EnumerateArray())
            {
                var target = ch.GetProperty("target");
                if (!target.TryGetProperty("node", out var nodeIdx)) continue;
                if (!nodeToJoint.TryGetValue(nodeIdx.GetInt32(), out int jointIndex)) continue;

                string pathStr = target.GetProperty("path").GetString() ?? "";
                AnimationPath path;
                switch (pathStr)
                {
                    case "translation": path = AnimationPath.Translation; break;
                    case "rotation": path = AnimationPath.Rotation; break;
                    case "scale": path = AnimationPath.Scale; break;
                    default: continue; // skip weights etc.
                }

                int samplerIdx = ch.GetProperty("sampler").GetInt32();
                var sampler = samplers[samplerIdx];

                int inputIdx = sampler.GetProperty("input").GetInt32();
                int outputIdx = sampler.GetProperty("output").GetInt32();

                var times = ReadScalarAccessor(accessors[inputIdx], bufferViews, bin);
                float[] values;
                if (path == AnimationPath.Rotation)
                {
                    var vec4s = ReadVec4Accessor(accessors[outputIdx], bufferViews, bin);
                    values = new float[vec4s.Length * 4];
                    for (int i = 0; i < vec4s.Length; i++)
                    {
                        values[i * 4] = vec4s[i].X; values[i * 4 + 1] = vec4s[i].Y;
                        values[i * 4 + 2] = vec4s[i].Z; values[i * 4 + 3] = vec4s[i].W;
                    }
                }
                else
                {
                    var vec3s = ReadVec3Accessor(accessors[outputIdx], bufferViews, bin);
                    values = new float[vec3s.Length * 3];
                    for (int i = 0; i < vec3s.Length; i++)
                    {
                        values[i * 3] = vec3s[i].X; values[i * 3 + 1] = vec3s[i].Y; values[i * 3 + 2] = vec3s[i].Z;
                    }
                }

                string interpStr = sampler.TryGetProperty("interpolation", out var interp)
                    ? interp.GetString() ?? "LINEAR" : "LINEAR";
                var interpolation = interpStr switch
                {
                    "STEP" => AnimationInterpolation.Step,
                    "CUBICSPLINE" => AnimationInterpolation.CubicSpline,
                    _ => AnimationInterpolation.Linear,
                };

                if (times.Length > 0)
                    maxTime = MathF.Max(maxTime, times[^1]);

                animChannels.Add(new AnimationChannel
                {
                    JointIndex = jointIndex,
                    Path = path,
                    Times = times,
                    Values = values,
                    Interpolation = interpolation,
                });
            }

            clips.Add(new AnimationClip
            {
                Name = name,
                Duration = maxTime,
                Channels = animChannels.ToArray(),
            });
        }

        return clips.ToArray();
    }

    private static byte[] ReadJointsAccessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
    {
        int count = accessor.GetProperty("count").GetInt32();
        int componentType = accessor.GetProperty("componentType").GetInt32();
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        int accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var view = bufferViews[viewIdx];
        int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
        int defaultStride = componentType == 5123 ? 8 : 4; // ushort vec4 = 8, ubyte vec4 = 4
        int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : defaultStride;

        var result = new byte[count * 4];
        int baseOffset = viewOffset + accessorOffset;

        for (int i = 0; i < count; i++)
        {
            int off = baseOffset + i * stride;
            if (componentType == 5121) // UNSIGNED_BYTE
            {
                result[i * 4] = bin[off];
                result[i * 4 + 1] = bin[off + 1];
                result[i * 4 + 2] = bin[off + 2];
                result[i * 4 + 3] = bin[off + 3];
            }
            else if (componentType == 5123) // UNSIGNED_SHORT
            {
                result[i * 4] = (byte)BitConverter.ToUInt16(bin, off);
                result[i * 4 + 1] = (byte)BitConverter.ToUInt16(bin, off + 2);
                result[i * 4 + 2] = (byte)BitConverter.ToUInt16(bin, off + 4);
                result[i * 4 + 3] = (byte)BitConverter.ToUInt16(bin, off + 6);
            }
        }
        return result;
    }

    private static Vector4[] ReadVec4Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
    {
        int count = accessor.GetProperty("count").GetInt32();
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        int accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var view = bufferViews[viewIdx];
        int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
        int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 16;

        var result = new Vector4[count];
        int baseOffset = viewOffset + accessorOffset;

        for (int i = 0; i < count; i++)
        {
            int off = baseOffset + i * stride;
            result[i] = new Vector4(
                BitConverter.ToSingle(bin, off),
                BitConverter.ToSingle(bin, off + 4),
                BitConverter.ToSingle(bin, off + 8),
                BitConverter.ToSingle(bin, off + 12));
        }
        return result;
    }

    private static Matrix4x4[] ReadMat4Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
    {
        int count = accessor.GetProperty("count").GetInt32();
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        int accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var view = bufferViews[viewIdx];
        int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
        int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 64;

        var result = new Matrix4x4[count];
        int baseOffset = viewOffset + accessorOffset;

        for (int i = 0; i < count; i++)
        {
            int off = baseOffset + i * stride;
            result[i] = new Matrix4x4(
                BitConverter.ToSingle(bin, off),      BitConverter.ToSingle(bin, off + 4),
                BitConverter.ToSingle(bin, off + 8),   BitConverter.ToSingle(bin, off + 12),
                BitConverter.ToSingle(bin, off + 16),  BitConverter.ToSingle(bin, off + 20),
                BitConverter.ToSingle(bin, off + 24),  BitConverter.ToSingle(bin, off + 28),
                BitConverter.ToSingle(bin, off + 32),  BitConverter.ToSingle(bin, off + 36),
                BitConverter.ToSingle(bin, off + 40),  BitConverter.ToSingle(bin, off + 44),
                BitConverter.ToSingle(bin, off + 48),  BitConverter.ToSingle(bin, off + 52),
                BitConverter.ToSingle(bin, off + 56),  BitConverter.ToSingle(bin, off + 60));
        }
        return result;
    }

    private static float[] ReadScalarAccessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
    {
        int count = accessor.GetProperty("count").GetInt32();
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        int accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var view = bufferViews[viewIdx];
        int viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
        int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 4;

        var result = new float[count];
        int baseOffset = viewOffset + accessorOffset;

        for (int i = 0; i < count; i++)
            result[i] = BitConverter.ToSingle(bin, baseOffset + i * stride);

        return result;
    }
}
