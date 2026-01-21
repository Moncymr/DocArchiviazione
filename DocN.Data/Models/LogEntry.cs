using System;

namespace DocN.Data.Models;

public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty; // Info, Warning, Error, Debug
    public string Category { get; set; } = string.Empty; // Upload, Embedding, AI, etc.
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? UserId { get; set; }
    public string? FileName { get; set; }
    public string? StackTrace { get; set; }
}
