using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// A parsed script command ready for execution each frame.
/// </summary>
public sealed class ScriptCommand
{
    public string Name { get; init; } = "";
    public float[] Args { get; init; } = [];
    public string StringArg { get; init; } = "";
}

/// <summary>
/// Runtime state for a node's script during play mode.
/// </summary>
public sealed class ScriptState
{
    public bool Entered { get; set; }
}

/// <summary>
/// Parses and executes simple per-node scripts in play mode.
///
/// Supported commands (one per line):
///   Rotate(x, y, z)           — rotate by (x,y,z) degrees per second
///   Bob(speed, height)        — oscillate vertically
///   FollowPlayer(speed, range)— move toward player if within range
///   OnEnter(message)          — show message when player enters node volume
///   OnEnterToggle(targetName) — toggle visibility of named target on enter
///   SetColor(r, g, b)         — override node color (0-1 range)
///   Scale(speed)              — pulse scale over time
///   Orbit(radius, speed)      — orbit around start position
///   LookAtPlayer()            — face the player each frame
/// </summary>
public static class ScriptEngine
{
    /// <summary>Parse script text into commands.</summary>
    public static List<ScriptCommand> Parse(string? script)
    {
        var commands = new List<ScriptCommand>();
        if (string.IsNullOrWhiteSpace(script)) return commands;

        foreach (var rawLine in script.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("//") || line.Length == 0) continue;

            // Parse: Name(arg1, arg2, ...) or Name("string")
            int paren = line.IndexOf('(');
            if (paren < 0) continue;
            int endParen = line.LastIndexOf(')');
            if (endParen <= paren) continue;

            var name = line[..paren].Trim();
            var argStr = line[(paren + 1)..endParen].Trim();

            // Check if it's a string argument (quoted)
            if (argStr.StartsWith('"') && argStr.EndsWith('"'))
            {
                commands.Add(new ScriptCommand
                {
                    Name = name,
                    StringArg = argStr[1..^1],
                });
            }
            else
            {
                // Parse numeric args
                var args = new List<float>();
                foreach (var part in argStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (float.TryParse(part, System.Globalization.CultureInfo.InvariantCulture, out float val))
                        args.Add(val);
                    else
                        args.Add(0); // default for unparseable
                }
                commands.Add(new ScriptCommand
                {
                    Name = name,
                    Args = args.ToArray(),
                    StringArg = argStr, // keep raw for string commands like OnEnterToggle
                });
            }
        }
        return commands;
    }

    /// <summary>
    /// Execute a node's parsed script commands for one frame.
    /// </summary>
    public static void Execute(
        SceneNode node,
        List<ScriptCommand> commands,
        ScriptState state,
        Vector3 startPos,
        float dt,
        float time,
        Vector3 playerPos,
        Scene scene,
        Action<string, float>? showMessage = null)
    {
        foreach (var cmd in commands)
        {
            float Arg(int i, float def = 0) => i < cmd.Args.Length ? cmd.Args[i] : def;

            switch (cmd.Name)
            {
                case "Rotate":
                {
                    float rx = Arg(0) * dt * MathF.PI / 180f;
                    float ry = Arg(1) * dt * MathF.PI / 180f;
                    float rz = Arg(2) * dt * MathF.PI / 180f;
                    var delta = Quaternion.CreateFromYawPitchRoll(ry, rx, rz);
                    node.Transform.Rotation = Quaternion.Normalize(delta * node.Transform.Rotation);
                    break;
                }

                case "Bob":
                {
                    float speed = Arg(0, 2f);
                    float height = Arg(1, 0.5f);
                    node.Transform.Position = startPos + new Vector3(0, MathF.Sin(time * speed) * height, 0);
                    break;
                }

                case "FollowPlayer":
                {
                    float speed = Arg(0, 3f);
                    float range = Arg(1, 15f);
                    var toPlayer = playerPos - node.Transform.Position;
                    toPlayer.Y = 0;
                    float dist = toPlayer.Length();
                    if (dist > 0.5f && dist < range)
                    {
                        var dir = toPlayer / dist;
                        node.Transform.Position += dir * MathF.Min(speed * dt, dist - 0.4f);
                    }
                    break;
                }

                case "OnEnter":
                {
                    scene.UpdateTransforms();
                    var aabb = FpsCamera.ComputeWorldAABB(node.Transform.WorldMatrix);
                    bool inside = playerPos.X > aabb.Min.X && playerPos.X < aabb.Max.X
                               && playerPos.Y > aabb.Min.Y && playerPos.Y < aabb.Max.Y + 1.8f
                               && playerPos.Z > aabb.Min.Z && playerPos.Z < aabb.Max.Z;
                    if (inside && !state.Entered)
                    {
                        state.Entered = true;
                        var msg = !string.IsNullOrWhiteSpace(cmd.StringArg) ? cmd.StringArg : "Entered";
                        showMessage?.Invoke(msg, 3f);
                    }
                    else if (!inside)
                    {
                        state.Entered = false;
                    }
                    break;
                }

                case "OnEnterToggle":
                {
                    scene.UpdateTransforms();
                    var aabb = FpsCamera.ComputeWorldAABB(node.Transform.WorldMatrix);
                    bool inside = playerPos.X > aabb.Min.X && playerPos.X < aabb.Max.X
                               && playerPos.Y > aabb.Min.Y && playerPos.Y < aabb.Max.Y + 1.8f
                               && playerPos.Z > aabb.Min.Z && playerPos.Z < aabb.Max.Z;
                    if (inside && !state.Entered)
                    {
                        state.Entered = true;
                        var target = scene.FindByName(cmd.StringArg);
                        if (target is not null) target.Visible = !target.Visible;
                    }
                    else if (!inside)
                    {
                        state.Entered = false;
                    }
                    break;
                }

                case "SetColor":
                {
                    node.Color = new Vector4(Arg(0, 1), Arg(1, 1), Arg(2, 1), 1);
                    break;
                }

                case "Scale":
                {
                    float speed = Arg(0, 2f);
                    float factor = 1f + MathF.Sin(time * speed) * 0.3f;
                    var baseScale = node.Transform.Scale;
                    // Pulse uniformly
                    node.Transform.Scale = new Vector3(factor);
                    break;
                }

                case "Orbit":
                {
                    float radius = Arg(0, 3f);
                    float speed = Arg(1, 1f);
                    float x = startPos.X + MathF.Cos(time * speed) * radius;
                    float z = startPos.Z + MathF.Sin(time * speed) * radius;
                    node.Transform.Position = new Vector3(x, node.Transform.Position.Y, z);
                    break;
                }

                case "LookAtPlayer":
                {
                    var dir = playerPos - node.Transform.Position;
                    dir.Y = 0;
                    if (dir.LengthSquared() > 0.01f)
                    {
                        dir = Vector3.Normalize(dir);
                        float yaw = MathF.Atan2(dir.X, dir.Z);
                        node.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
                    }
                    break;
                }
            }
        }
    }
}
