using WgpuSharp.Core;

namespace WgpuSharp.Tests;

public class DeviceLostTests
{
    [Fact]
    public void DeviceLostInfo_RecordEquality()
    {
        var a = new DeviceLostInfo("destroyed", "GPU device was destroyed");
        var b = new DeviceLostInfo("destroyed", "GPU device was destroyed");
        Assert.Equal(a, b);
    }

    [Fact]
    public void DeviceLostInfo_PropertiesRoundTrip()
    {
        var info = new DeviceLostInfo("unknown", "Driver crash");
        Assert.Equal("unknown", info.Reason);
        Assert.Equal("Driver crash", info.Message);
    }

    [Fact]
    public void DeviceLostInfo_DifferentValues_NotEqual()
    {
        var a = new DeviceLostInfo("destroyed", "Reason A");
        var b = new DeviceLostInfo("unknown", "Reason B");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DeviceLostInfo_ToString_ContainsValues()
    {
        var info = new DeviceLostInfo("destroyed", "Device lost");
        var str = info.ToString();
        Assert.Contains("destroyed", str);
        Assert.Contains("Device lost", str);
    }
}
