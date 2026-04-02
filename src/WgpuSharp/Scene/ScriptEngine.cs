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
    public readonly HashSet<string> FiredEvents = new();
}

/// <summary>
/// An event-driven rule: When(condition) -> Then(action).
/// Parsed from script lines like: When AllCollected -> ShowMessage("You win!")
/// </summary>
public sealed class EventRule
{
    public string Condition { get; init; } = "";
    public float ConditionArg { get; init; }
    public string ConditionStringArg { get; init; } = "";
    public string Action { get; init; } = "";
    public float[] ActionArgs { get; init; } = [];
    public string ActionStringArg { get; init; } = "";
    public bool Once { get; init; } = true; // most events fire once
}

/// <summary>
/// Parses and executes simple per-node scripts in play mode.
///
/// Supported commands (one per line):
///   Rotate(x, y, z)           — rotate by (x,y,z) degrees per second
///   Bob(speed, height)        — oscillate vertically
///   FollowPlayer(speed, range)— move toward player if within range
///   FleePlayer(speed, range)  — move away from player if within range
///   OnEnter(message)          — show message when player enters node volume
///   OnEnterToggle(targetName) — toggle visibility of named target on enter
///   OnEnterDestroy()          — destroy this node when player enters
///   SetColor(r, g, b)         — override node color (0-1 range)
///   ColorPulse(r, g, b, speed)— pulse between current color and target
///   Scale(speed)              — pulse scale over time
///   Orbit(radius, speed)      — orbit around start position
///   LookAtPlayer()            — face the player each frame
///   MoveToward(x, y, z, speed)— move toward a world position
///   Patrol(x1,z1, x2,z2, speed) — patrol back and forth between two XZ points
///   Spawn(meshType, delay)    — periodically spawn a child node (collectible/enemy)
///   IfNear(range, targetName) — toggle target visibility when player is near
///   Hide()                    — make this node invisible
///   Show(targetName)          — make a named node visible
///   Shake(intensity, speed)   — vibrate the node around its start position
///   PingPong(x, y, z, speed)  — move linearly between startPos and startPos+offset
///   Grow(minScale, maxScale, speed) — pulse scale between min and max
///   FaceDirection(x, y, z)    — lock rotation to face a specific direction
///   Wander(radius, speed)     — organic wandering within a radius
///   DelayedHide(seconds)      — hide the node after N seconds
///   Spawn(name)               — make a named node visible (re-enable it)
///   Flash(r, g, b, speed)     — alternate between original color and given color
///   Gravity(strength)         — make the node fall with custom gravity
///   RotateToPlayer(speed)     — smoothly rotate to face the player with lerp
/// </summary>
public static class ScriptEngine
{
    /// <summary>Parse script text into commands and event rules.</summary>
    public static (List<ScriptCommand> commands, List<EventRule> rules) ParseFull(string? script)
    {
        var commands = new List<ScriptCommand>();
        var rules = new List<EventRule>();
        if (string.IsNullOrWhiteSpace(script)) return (commands, rules);

        foreach (var rawLine in script.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("//") || line.Length == 0) continue;

            // Check for event rule: When <condition> -> <action>
            if (line.StartsWith("When ", StringComparison.OrdinalIgnoreCase) && line.Contains("->"))
            {
                var arrow = line.IndexOf("->");
                var condPart = line[5..arrow].Trim();
                var actPart = line[(arrow + 2)..].Trim();
                rules.Add(ParseEventRule(condPart, actPart));
                continue;
            }

            // Regular command
            var cmd = ParseCommand(line);
            if (cmd is not null) commands.Add(cmd);
        }

        return (commands, rules);
    }

    /// <summary>Parse script text into commands (legacy — ignores event rules).</summary>
    public static List<ScriptCommand> Parse(string? script) => ParseFull(script).commands;

    private static EventRule ParseEventRule(string condition, string action)
    {
        // Parse condition: AllCollected, Score(N), HealthBelow(N), TimerBelow(N), KeyCollected("name"), Enter, Near(range)
        float condArg = 0;
        string condStr = "";
        var condName = condition;
        int cp = condition.IndexOf('(');
        if (cp >= 0)
        {
            condName = condition[..cp].Trim();
            var cArgStr = condition[(cp + 1)..].TrimEnd(')').Trim();
            if (cArgStr.StartsWith('"') && cArgStr.EndsWith('"'))
                condStr = cArgStr[1..^1];
            else
                float.TryParse(cArgStr, System.Globalization.CultureInfo.InvariantCulture, out condArg);
        }

        // Parse action: same as a regular command
        var actCmd = ParseCommand(action);
        return new EventRule
        {
            Condition = condName,
            ConditionArg = condArg,
            ConditionStringArg = condStr,
            Action = actCmd?.Name ?? action,
            ActionArgs = actCmd?.Args ?? [],
            ActionStringArg = actCmd?.StringArg ?? "",
        };
    }

    private static ScriptCommand? ParseCommand(string line)
    {
        int paren = line.IndexOf('(');
        if (paren < 0) return null;
        int endParen = line.LastIndexOf(')');
        if (endParen <= paren) return null;

        var name = line[..paren].Trim();
        var argStr = line[(paren + 1)..endParen].Trim();

        if (argStr.StartsWith('"') && argStr.EndsWith('"'))
        {
            return new ScriptCommand { Name = name, StringArg = argStr[1..^1] };
        }

        var args = new List<float>();
        foreach (var part in argStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (float.TryParse(part, System.Globalization.CultureInfo.InvariantCulture, out float val))
                args.Add(val);
            else
                args.Add(0);
        }
        return new ScriptCommand { Name = name, Args = args.ToArray(), StringArg = argStr };
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
                    // Transforms already updated once per frame by caller
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
                    // Transforms already updated once per frame by caller
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

                case "FleePlayer":
                {
                    float speed = Arg(0, 3f);
                    float range = Arg(1, 10f);
                    var away = node.Transform.Position - playerPos;
                    away.Y = 0;
                    float dist = away.Length();
                    if (dist < range && dist > 0.1f)
                    {
                        var dir = away / dist;
                        node.Transform.Position += dir * speed * dt;
                    }
                    break;
                }

                case "OnEnterDestroy":
                {
                    // Transforms already updated once per frame by caller
                    var aabb = FpsCamera.ComputeWorldAABB(node.Transform.WorldMatrix);
                    bool inside = playerPos.X > aabb.Min.X && playerPos.X < aabb.Max.X
                               && playerPos.Y > aabb.Min.Y && playerPos.Y < aabb.Max.Y + 1.8f
                               && playerPos.Z > aabb.Min.Z && playerPos.Z < aabb.Max.Z;
                    if (inside) node.Visible = false;
                    break;
                }

                case "ColorPulse":
                {
                    float r = Arg(0, 1), g = Arg(1, 0), b = Arg(2, 0), spd = Arg(3, 2);
                    float t = (MathF.Sin(time * spd) + 1f) * 0.5f;
                    var target = new Vector4(r, g, b, 1);
                    var original = node.Color;
                    node.Color = Vector4.Lerp(original, target, t);
                    break;
                }

                case "MoveToward":
                {
                    var target = new Vector3(Arg(0), Arg(1), Arg(2));
                    float speed = Arg(3, 2f);
                    var toTarget = target - node.Transform.Position;
                    float dist = toTarget.Length();
                    if (dist > 0.05f)
                        node.Transform.Position += (toTarget / dist) * MathF.Min(speed * dt, dist);
                    break;
                }

                case "Patrol":
                {
                    // Patrol(x1, z1, x2, z2, speed)
                    var a = new Vector3(Arg(0), startPos.Y, Arg(1));
                    var b = new Vector3(Arg(2), startPos.Y, Arg(3));
                    float speed = Arg(4, 2f);
                    if (Vector3.DistanceSquared(a, b) < 0.01f) break;
                    float period = Vector3.Distance(a, b) / MathF.Max(speed, 0.1f);
                    float t = (MathF.Sin(time * MathF.PI / period) + 1f) * 0.5f;
                    node.Transform.Position = Vector3.Lerp(a, b, t);
                    break;
                }

                case "IfNear":
                {
                    float range = Arg(0, 5f);
                    var targetName = cmd.StringArg;
                    var target = scene.FindByName(targetName);
                    if (target is not null)
                    {
                        float dist = Vector3.Distance(playerPos, node.Transform.Position);
                        target.Visible = dist < range;
                    }
                    break;
                }

                case "Hide":
                    node.Visible = false;
                    break;

                case "Show":
                {
                    var target = scene.FindByName(cmd.StringArg);
                    if (target is not null) target.Visible = true;
                    break;
                }

                case "Shake":
                {
                    float intensity = Arg(0, 0.1f);
                    float shakeSpd = Arg(1, 10f);
                    var offset = new Vector3(
                        MathF.Sin(time * shakeSpd * 7.1f) * intensity,
                        MathF.Sin(time * shakeSpd * 5.3f) * intensity,
                        MathF.Sin(time * shakeSpd * 6.7f) * intensity);
                    node.Transform.Position = startPos + offset;
                    break;
                }

                case "PingPong":
                {
                    var pingTarget = startPos + new Vector3(Arg(0), Arg(1), Arg(2));
                    float pingSpeed = Arg(3, 1f);
                    float pingT = (MathF.Sin(time * pingSpeed) + 1f) * 0.5f;
                    node.Transform.Position = Vector3.Lerp(startPos, pingTarget, pingT);
                    break;
                }

                case "Grow":
                {
                    float minScale = Arg(0, 0.5f);
                    float maxScale = Arg(1, 1.5f);
                    float growSpeed = Arg(2, 2f);
                    float growT = (MathF.Sin(time * growSpeed) + 1f) * 0.5f;
                    float s = minScale + (maxScale - minScale) * growT;
                    node.Transform.Scale = new Vector3(s);
                    break;
                }

                case "FaceDirection":
                {
                    var faceDir = new Vector3(Arg(0), Arg(1), Arg(2, 1f));
                    if (faceDir.LengthSquared() > 0.001f)
                    {
                        faceDir = Vector3.Normalize(faceDir);
                        float yaw = MathF.Atan2(faceDir.X, faceDir.Z);
                        node.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
                    }
                    break;
                }

                case "Wander":
                {
                    float radius = Arg(0, 3f);
                    float wanderSpeed = Arg(1, 1f);
                    float wx = MathF.Sin(time * wanderSpeed * 1.3f + 0.0f) * radius;
                    float wz = MathF.Sin(time * wanderSpeed * 0.9f + 2.7f) * radius;
                    node.Transform.Position = startPos + new Vector3(wx, 0, wz);
                    break;
                }

                case "DelayedHide":
                {
                    float delay = Arg(0, 3f);
                    if (time >= delay)
                        node.Visible = false;
                    break;
                }

                case "Spawn":
                {
                    var spawnTarget = scene.FindByName(cmd.StringArg);
                    if (spawnTarget is not null) spawnTarget.Visible = true;
                    break;
                }

                case "Flash":
                {
                    float fr = Arg(0, 1), fg = Arg(1, 1), fb = Arg(2, 1), flashSpd = Arg(3, 2f);
                    float flashT = (MathF.Sin(time * flashSpd) + 1f) * 0.5f;
                    var flashColor = new Vector4(fr, fg, fb, 1);
                    var originalColor = node.Color;
                    node.Color = Vector4.Lerp(originalColor, flashColor, flashT);
                    break;
                }

                case "Gravity":
                {
                    float strength = Arg(0, 9.8f);
                    // Simple free-fall from start position: y = y0 - 0.5 * g * t^2
                    float y = startPos.Y - 0.5f * strength * time * time;
                    node.Transform.Position = new Vector3(startPos.X, y, startPos.Z);
                    break;
                }

                case "RotateToPlayer":
                {
                    float rotSpeed = Arg(0, 2f);
                    var toPlayer = playerPos - node.Transform.Position;
                    toPlayer.Y = 0;
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        var dir = Vector3.Normalize(toPlayer);
                        float targetYaw = MathF.Atan2(dir.X, dir.Z);
                        var targetRot = Quaternion.CreateFromYawPitchRoll(targetYaw, 0, 0);
                        node.Transform.Rotation = Quaternion.Slerp(node.Transform.Rotation, targetRot, MathF.Min(rotSpeed * dt, 1f));
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Evaluate event rules. Call once per frame with current game state.
    /// Conditions: AllCollected, Score(N), HealthBelow(N), TimerBelow(N), KeyCollected("name"), Enter, Near(range),
    ///             PlayerGrounded, NodeVisible("name"), NodeHidden("name")
    /// Actions: ShowMessage("text"), Toggle("name"), Show("name"), Hide("name"), Destroy("name"),
    ///          TeleportPlayer(x,y,z), WinGame(), LoseGame(), AddScore(N), Heal(N), Damage(N),
    ///          SetTag("name", "tag"), PlaySound("id")
    /// </summary>
    public static void EvaluateEvents(
        SceneNode node,
        List<EventRule> rules,
        ScriptState state,
        Vector3 playerPos,
        Scene scene,
        int score, int totalCollectibles, int health, float timer,
        HashSet<string> collectedKeys,
        bool playerGrounded = false,
        Action<string, float>? showMessage = null,
        Action<int>? addScore = null,
        Action<int>? setHealth = null,
        Action? winGame = null,
        Action? loseGame = null,
        Action<Vector3>? teleportPlayer = null)
    {
        foreach (var rule in rules)
        {
            string ruleId = $"{rule.Condition}_{rule.Action}";
            if (rule.Once && state.FiredEvents.Contains(ruleId)) continue;

            bool conditionMet = rule.Condition switch
            {
                "AllCollected" => totalCollectibles > 0 && score >= totalCollectibles,
                "Score" => score >= (int)rule.ConditionArg,
                "HealthBelow" => health < (int)rule.ConditionArg,
                "TimerBelow" => timer < rule.ConditionArg,
                "KeyCollected" => collectedKeys.Contains(rule.ConditionStringArg),
                "Enter" => IsPlayerInside(node, playerPos, scene),
                "Near" => Vector3.Distance(playerPos, node.Transform.WorldMatrix.Translation) < rule.ConditionArg,
                "PlayerGrounded" => playerGrounded,
                "NodeVisible" => scene.FindByName(rule.ConditionStringArg) is { Visible: true },
                "NodeHidden" => scene.FindByName(rule.ConditionStringArg) is null or { Visible: false },
                _ => false,
            };

            if (!conditionMet) continue;

            if (rule.Once) state.FiredEvents.Add(ruleId);

            // Fire action
            float AArg(int i, float def = 0) => i < rule.ActionArgs.Length ? rule.ActionArgs[i] : def;
            switch (rule.Action)
            {
                case "ShowMessage":
                    showMessage?.Invoke(rule.ActionStringArg, 3f);
                    break;
                case "Toggle":
                    var toggleTarget = scene.FindByName(rule.ActionStringArg);
                    if (toggleTarget is not null) toggleTarget.Visible = !toggleTarget.Visible;
                    break;
                case "Show":
                    var showTarget = scene.FindByName(rule.ActionStringArg);
                    if (showTarget is not null) showTarget.Visible = true;
                    break;
                case "Hide":
                    var hideTarget = scene.FindByName(rule.ActionStringArg);
                    if (hideTarget is not null) hideTarget.Visible = false;
                    break;
                case "Destroy":
                    var destroyTarget = scene.FindByName(rule.ActionStringArg);
                    if (destroyTarget is not null) destroyTarget.Visible = false;
                    break;
                case "TeleportPlayer":
                    teleportPlayer?.Invoke(new Vector3(AArg(0), AArg(1), AArg(2)));
                    break;
                case "WinGame":
                    showMessage?.Invoke("YOU WIN!", 10f);
                    winGame?.Invoke();
                    break;
                case "LoseGame":
                    loseGame?.Invoke();
                    break;
                case "AddScore":
                    addScore?.Invoke((int)AArg(0, 1));
                    break;
                case "Heal":
                    setHealth?.Invoke(Math.Min(100, health + (int)AArg(0, 25)));
                    break;
                case "Damage":
                    setHealth?.Invoke(Math.Max(0, health - (int)AArg(0, 10)));
                    break;
                case "SetTag":
                {
                    // Parse "NodeName, TagValue" from the string argument
                    var parts = rule.ActionStringArg.Split(',', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        var tagNode = scene.FindByName(parts[0]);
                        if (tagNode is not null && Enum.TryParse<NodeTag>(parts[1], ignoreCase: true, out var newTag))
                            tagNode.Tag = newTag;
                    }
                    break;
                }
                case "PlaySound":
                    showMessage?.Invoke($"[Sound: {rule.ActionStringArg}]", 2f);
                    break;
            }
        }
    }

    private static bool IsPlayerInside(SceneNode node, Vector3 playerPos, Scene scene)
    {
        var aabb = FpsCamera.ComputeWorldAABB(node.Transform.WorldMatrix);
        return playerPos.X > aabb.Min.X && playerPos.X < aabb.Max.X
            && playerPos.Y > aabb.Min.Y && playerPos.Y < aabb.Max.Y + 1.8f
            && playerPos.Z > aabb.Min.Z && playerPos.Z < aabb.Max.Z;
    }
}
