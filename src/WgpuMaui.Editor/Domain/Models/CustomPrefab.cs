namespace WgpuMaui.Editor.Domain.Models;

public sealed class CustomPrefab
{
    public string Name { get; set; } = "";

    public List<PrefabNodeData> Nodes { get; set; } = [];
}