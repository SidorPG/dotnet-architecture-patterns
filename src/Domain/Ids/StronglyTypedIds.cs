// Strongly-typed IDs — each struct wraps a Guid so the compiler rejects mixing up
// StudentId with GroupId. In a production codebase you'd add the StronglyTypedId
// source-generator package to generate JSON converters and EF Core value converters
// automatically; here we keep them as plain readonly record structs for clarity.

namespace Domain.Ids;

public readonly record struct GroupJoinRequestId(Guid Value)
{
    public static GroupJoinRequestId New() => new(Guid.NewGuid());
    public static GroupJoinRequestId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}

public readonly record struct ProcessManagerId(Guid Value)
{
    public static ProcessManagerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct StudentId(Guid Value)
{
    public static StudentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct GroupId(Guid Value)
{
    public static GroupId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct InstructorId(Guid Value)
{
    public static InstructorId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
