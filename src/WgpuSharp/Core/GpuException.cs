namespace WgpuSharp.Core;

/// <summary>
/// Exception thrown when a WebGPU operation fails.
/// </summary>
public class GpuException : Exception
{
    public GpuException(string message) : base(message) { }
    public GpuException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when a WGSL shader fails to compile.
/// Contains structured error messages with line numbers.
/// </summary>
public class ShaderCompilationException : GpuException
{
    /// <summary>Compilation messages (errors, warnings, info).</summary>
    public ShaderMessage[] Messages { get; }

    public ShaderCompilationException(string summary, ShaderMessage[] messages)
        : base(summary)
    {
        Messages = messages;
    }

    public override string ToString()
    {
        if (Messages.Length == 0)
            return base.ToString();

        var lines = new System.Text.StringBuilder();
        lines.AppendLine(Message);
        foreach (var m in Messages)
        {
            lines.Append($"  [{m.Type}] ");
            if (m.LineNum > 0)
                lines.Append($"line {m.LineNum}:{m.LinePos} — ");
            lines.AppendLine(m.Message);
        }
        return lines.ToString();
    }
}

/// <summary>A single message from shader compilation (error, warning, or info).</summary>
public class ShaderMessage
{
    /// <summary>Message severity: "error", "warning", or "info".</summary>
    public string Type { get; set; } = "";
    /// <summary>The message text.</summary>
    public string Message { get; set; } = "";
    /// <summary>1-based line number in the shader source.</summary>
    public int LineNum { get; set; }
    /// <summary>1-based column position within the line.</summary>
    public int LinePos { get; set; }
    /// <summary>Byte offset in the source.</summary>
    public int Offset { get; set; }
    /// <summary>Length of the problematic source range.</summary>
    public int Length { get; set; }
}

/// <summary>Result of shader compilation info query.</summary>
public class ShaderCompilationInfo
{
    public ShaderMessage[] Messages { get; set; } = [];
}
