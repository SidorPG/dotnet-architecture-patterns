namespace Domain.Abstractions;

// Thrown by aggregates for invariant violations — not for "not found" or auth failures.
// Caught by DomainExceptionBehavior in the Application pipeline and converted to Result.Failure().
public class DomainException(string message) : Exception(message);
