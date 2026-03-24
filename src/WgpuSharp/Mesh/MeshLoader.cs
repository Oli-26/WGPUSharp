namespace WgpuSharp.Mesh;

public static class MeshLoader
{
    /// <summary>
    /// Loads meshes from a file stream, auto-detecting format by extension.
    /// Returns an array since GLB files can contain multiple meshes.
    /// </summary>
    public static Mesh[] Load(Stream stream, string fileName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".obj" => [ObjLoader.Load(stream)],
            ".glb" => GlbLoader.Load(stream),
            ".stl" => [StlLoader.Load(stream)],
            _ => throw new NotSupportedException($"Unsupported mesh format: '{ext}'. Supported formats: .obj, .glb, .stl"),
        };
    }

    /// <summary>
    /// Loads meshes from a byte array, auto-detecting format by extension.
    /// </summary>
    public static Mesh[] Load(byte[] data, string fileName)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            throw new ArgumentException("Mesh data cannot be empty.", nameof(data));
        using var stream = new MemoryStream(data);
        return Load(stream, fileName);
    }
}
