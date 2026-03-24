namespace WgpuSharp.Core;

/// <summary>Information about the GPU adapter hardware.</summary>
public class GpuAdapterInfo
{
    /// <summary>GPU vendor name (e.g. "intel", "nvidia", "amd").</summary>
    public string Vendor { get; set; } = "";
    /// <summary>GPU architecture (e.g. "gen-12lp").</summary>
    public string Architecture { get; set; } = "";
    /// <summary>GPU device identifier.</summary>
    public string Device { get; set; } = "";
    /// <summary>Human-readable GPU description.</summary>
    public string Description { get; set; } = "";

    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Description)) return Description;
        if (!string.IsNullOrEmpty(Vendor)) return $"{Vendor} {Architecture}".Trim();
        return "Unknown GPU";
    }
}

/// <summary>Hardware limits reported by the GPU adapter.</summary>
public class GpuAdapterLimits
{
    /// <summary>Maximum 2D texture dimension (width or height).</summary>
    public int MaxTextureDimension2D { get; set; }
    /// <summary>Maximum texture array layers.</summary>
    public int MaxTextureArrayLayers { get; set; }
    /// <summary>Maximum bind groups per pipeline.</summary>
    public int MaxBindGroups { get; set; }
    /// <summary>Maximum buffer size in bytes.</summary>
    public long MaxBufferSize { get; set; }
    /// <summary>Maximum vertex buffers per pipeline.</summary>
    public int MaxVertexBuffers { get; set; }
    /// <summary>Maximum compute workgroup size in X dimension.</summary>
    public int MaxComputeWorkgroupSizeX { get; set; }
    /// <summary>Maximum compute workgroup size in Y dimension.</summary>
    public int MaxComputeWorkgroupSizeY { get; set; }
    /// <summary>Maximum compute workgroup size in Z dimension.</summary>
    public int MaxComputeWorkgroupSizeZ { get; set; }
}
