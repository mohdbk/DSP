namespace Dsp.SharedKernel;

/// <summary>A business rule or invariant was violated.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>The requested resource does not exist (or is not visible to the caller).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The caller is authenticated but not allowed to perform the action.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>A field-level error, keyed by field.</summary>
public sealed record FieldError(string Field, string Message);

/// <summary>Input failed validation. Carries field-level errors.</summary>
public sealed class ValidationException(IReadOnlyList<FieldError> errors)
    : Exception("One or more fields are invalid.")
{
    public IReadOnlyList<FieldError> Errors { get; } = errors;
}
