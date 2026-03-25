using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Tests;

public class MsaaTests
{
    [Fact]
    public void MultisampleState_DefaultCount_Is4()
    {
        var ms = new MultisampleState();
        Assert.Equal(4, ms.Count);
    }

    [Fact]
    public void MultisampleState_CustomCount()
    {
        var ms = new MultisampleState { Count = 1 };
        Assert.Equal(1, ms.Count);
    }

    [Fact]
    public void TextureDescriptor_SampleCount_DefaultIs1()
    {
        var desc = new TextureDescriptor
        {
            Size = [800, 600],
            Format = TextureFormat.Rgba8Unorm,
            Usage = TextureUsage.RenderAttachment,
        };
        Assert.Equal(1, desc.SampleCount);
    }

    [Fact]
    public void TextureDescriptor_SampleCount_CanBeSet()
    {
        var desc = new TextureDescriptor
        {
            Size = [800, 600],
            Format = TextureFormat.Rgba8Unorm,
            Usage = TextureUsage.RenderAttachment,
            SampleCount = 4,
        };
        Assert.Equal(4, desc.SampleCount);
    }
}
