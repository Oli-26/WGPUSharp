using System.Numerics;
using System.Runtime.CompilerServices;
using WgpuSharp.Resources;

namespace WgpuSharp.Tests;

public class BufferWriteTests
{
    [Fact]
    public void ToBytes_Matrix4x4_Returns64Bytes()
    {
        var matrix = Matrix4x4.Identity;
        var bytes = BufferDataHelper.ToBytes(matrix);
        Assert.Equal(64, bytes.Length);
        // First float should be 1.0 (identity matrix [0,0])
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 0));
    }

    [Fact]
    public void ToBytes_Vector3Array_Returns12BytesPerElement()
    {
        var data = new[] { new Vector3(1, 2, 3), new Vector3(4, 5, 6) };
        var bytes = BufferDataHelper.ToBytes(data);
        Assert.Equal(24, bytes.Length); // 2 * 12 bytes
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 0));
        Assert.Equal(4f, BitConverter.ToSingle(bytes, 12));
    }

    [Fact]
    public void ToBytes_SingleFloat_Returns4Bytes()
    {
        var bytes = BufferDataHelper.ToBytes(42.5f);
        Assert.Equal(4, bytes.Length);
        Assert.Equal(42.5f, BitConverter.ToSingle(bytes, 0));
    }

    [Fact]
    public void ToBytes_EmptyArray_ReturnsEmptyBytes()
    {
        var bytes = BufferDataHelper.ToBytes(Array.Empty<float>());
        Assert.Empty(bytes);
    }

    [Fact]
    public void ToBytes_Vector4_Returns16Bytes()
    {
        var v = new Vector4(1, 2, 3, 4);
        var bytes = BufferDataHelper.ToBytes(v);
        Assert.Equal(16, bytes.Length);
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 0));
        Assert.Equal(4f, BitConverter.ToSingle(bytes, 12));
    }

    [Fact]
    public void ToBytes_IntArray_ReturnsCorrectBytes()
    {
        var data = new[] { 1, 2, 3 };
        var bytes = BufferDataHelper.ToBytes(data);
        Assert.Equal(12, bytes.Length);
        Assert.Equal(1, BitConverter.ToInt32(bytes, 0));
        Assert.Equal(3, BitConverter.ToInt32(bytes, 8));
    }

    [Fact]
    public void ToBytes_UintArray_ReturnsCorrectBytes()
    {
        var data = new uint[] { 0, 1, 2, 100 };
        var bytes = BufferDataHelper.ToBytes(data);
        Assert.Equal(16, bytes.Length);
        Assert.Equal(100u, BitConverter.ToUInt32(bytes, 12));
    }

    [Fact]
    public void ToBytes_CustomStruct_ReturnsCorrectSize()
    {
        var s = new TestUniform { A = 1.0f, B = 2.0f, C = 3 };
        var bytes = BufferDataHelper.ToBytes(s);
        Assert.Equal(Unsafe.SizeOf<TestUniform>(), bytes.Length);
        Assert.Equal(1.0f, BitConverter.ToSingle(bytes, 0));
        Assert.Equal(2.0f, BitConverter.ToSingle(bytes, 4));
        Assert.Equal(3, BitConverter.ToInt32(bytes, 8));
    }

    private struct TestUniform
    {
        public float A;
        public float B;
        public int C;
    }
}
