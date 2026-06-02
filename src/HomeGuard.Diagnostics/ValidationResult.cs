namespace HomeGuard.Diagnostics;

public enum ValidationSeverity { Ok, Info, Warning, Error }

/// <summary>
/// Result of a single startup configuration check.
/// </summary>
/// <param name="Severity">How bad is it?</param>
/// <param name="Area">Short tag: "Database", "CORS", "FileStorage", etc.</param>
/// <param name="Message">One-liner: what is wrong or confirmed OK.</param>
/// <param name="Detail">Optional: path/key/value that caused the issue.</param>
/// <param name="FixHint">How to fix it: what file to open, what key to set.</param>
/// <param name="AffectedFeature">What stops working if this is ignored.</param>
/// <param name="DeployNote">What to check/set in the production/container environment.</param>
public sealed record ValidationResult(
    ValidationSeverity Severity,
    string Area,
    string Message,
    string? Detail = null,
    string? FixHint = null,
    string? AffectedFeature = null,
    string? DeployNote = null);
