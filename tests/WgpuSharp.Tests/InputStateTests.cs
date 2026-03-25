using WgpuSharp.Core;

namespace WgpuSharp.Tests;

public class InputStateTests
{
    [Fact]
    public void IsKeyDown_WhenKeyInArray_ReturnsTrue()
    {
        var state = new InputState { Keys = ["KeyW", "Space", "ShiftLeft"] };
        Assert.True(state.IsKeyDown("KeyW"));
        Assert.True(state.IsKeyDown("Space"));
    }

    [Fact]
    public void IsKeyDown_WhenKeyNotInArray_ReturnsFalse()
    {
        var state = new InputState { Keys = ["KeyW"] };
        Assert.False(state.IsKeyDown("KeyA"));
    }

    [Fact]
    public void IsKeyDown_EmptyKeys_ReturnsFalse()
    {
        var state = new InputState();
        Assert.False(state.IsKeyDown("KeyW"));
    }

    [Fact]
    public void IsMouseButtonDown_LeftButton_ChecksBit0()
    {
        var state = new InputState { MouseButtons = 1 }; // bit 0 set
        Assert.True(state.IsMouseButtonDown(0));
        Assert.False(state.IsMouseButtonDown(1));
        Assert.False(state.IsMouseButtonDown(2));
    }

    [Fact]
    public void IsMouseButtonDown_RightButton_ChecksBit2()
    {
        var state = new InputState { MouseButtons = 4 }; // bit 2 set
        Assert.False(state.IsMouseButtonDown(0));
        Assert.True(state.IsMouseButtonDown(2));
    }

    [Fact]
    public void IsMouseButtonDown_MultipleButtons_AllDetected()
    {
        var state = new InputState { MouseButtons = 5 }; // bits 0 and 2
        Assert.True(state.IsMouseButtonDown(0));
        Assert.False(state.IsMouseButtonDown(1));
        Assert.True(state.IsMouseButtonDown(2));
    }

    [Fact]
    public void LeftMouseDown_Property_MatchesBit0()
    {
        Assert.True(new InputState { MouseButtons = 1 }.LeftMouseDown);
        Assert.False(new InputState { MouseButtons = 4 }.LeftMouseDown);
        Assert.False(new InputState { MouseButtons = 0 }.LeftMouseDown);
    }

    [Fact]
    public void RightMouseDown_Property_MatchesBit2()
    {
        Assert.True(new InputState { MouseButtons = 4 }.RightMouseDown);
        Assert.False(new InputState { MouseButtons = 1 }.RightMouseDown);
        Assert.False(new InputState { MouseButtons = 0 }.RightMouseDown);
    }

    [Fact]
    public void DefaultState_AllZeroed()
    {
        var state = new InputState();
        Assert.Empty(state.Keys);
        Assert.Equal(0f, state.MouseX);
        Assert.Equal(0f, state.MouseY);
        Assert.Equal(0f, state.MouseDX);
        Assert.Equal(0f, state.MouseDY);
        Assert.Equal(0, state.MouseButtons);
        Assert.Equal(0f, state.WheelDelta);
        Assert.False(state.PointerLocked);
    }

    [Fact]
    public void MousePosition_StoresCorrectly()
    {
        var state = new InputState { MouseX = 100.5f, MouseY = 200.3f };
        Assert.Equal(100.5f, state.MouseX);
        Assert.Equal(200.3f, state.MouseY);
    }
}
