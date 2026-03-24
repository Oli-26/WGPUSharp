using System.Globalization;
using System.Numerics;

namespace WgpuSharp.Mesh;

public static class MtlLoader
{
    public static Dictionary<string, Material> Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Load(reader);
    }

    public static Dictionary<string, Material> Load(string mtlText)
    {
        using var reader = new StringReader(mtlText);
        return Load(reader);
    }

    private static Dictionary<string, Material> Load(TextReader reader)
    {
        var materials = new Dictionary<string, Material>();
        string? currentName = null;
        var baseColor = Vector4.One;
        float metallic = 0f;
        float roughness = 1f;
        var emissive = Vector3.Zero;

        void SaveCurrent()
        {
            if (currentName is not null)
            {
                materials[currentName] = new Material
                {
                    Name = currentName,
                    BaseColor = baseColor,
                    Metallic = metallic,
                    Roughness = roughness,
                    Emissive = emissive,
                };
            }
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "newmtl" when parts.Length >= 2:
                    SaveCurrent();
                    currentName = parts[1];
                    baseColor = Vector4.One;
                    metallic = 0f;
                    roughness = 1f;
                    emissive = Vector3.Zero;
                    break;

                case "Kd" when parts.Length >= 4:
                    // Diffuse color → base color
                    baseColor = new Vector4(
                        ParseFloat(parts[1]),
                        ParseFloat(parts[2]),
                        ParseFloat(parts[3]),
                        baseColor.W);
                    break;

                case "d" when parts.Length >= 2:
                    // Dissolve (opacity)
                    baseColor = new Vector4(baseColor.X, baseColor.Y, baseColor.Z, ParseFloat(parts[1]));
                    break;

                case "Tr" when parts.Length >= 2:
                    // Transparency (inverse of dissolve)
                    baseColor = new Vector4(baseColor.X, baseColor.Y, baseColor.Z, 1f - ParseFloat(parts[1]));
                    break;

                case "Ke" when parts.Length >= 4:
                    emissive = new Vector3(
                        ParseFloat(parts[1]),
                        ParseFloat(parts[2]),
                        ParseFloat(parts[3]));
                    break;

                case "Ns" when parts.Length >= 2:
                    // Specular exponent → roughness approximation
                    // Ns ranges 0-1000, higher = shinier = less rough
                    float ns = ParseFloat(parts[1]);
                    roughness = 1f - MathF.Min(ns / 1000f, 1f);
                    break;

                case "Pm" when parts.Length >= 2:
                    // Metallic (PBR extension)
                    metallic = ParseFloat(parts[1]);
                    break;
            }
        }

        SaveCurrent();
        return materials;
    }

    private static float ParseFloat(string s)
        => float.Parse(s, CultureInfo.InvariantCulture);
}
