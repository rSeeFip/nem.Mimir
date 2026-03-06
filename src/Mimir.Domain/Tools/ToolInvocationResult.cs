namespace Mimir.Domain.Tools;

public sealed record ToolInvocationResult(bool Success, string? Content, string? Error);
