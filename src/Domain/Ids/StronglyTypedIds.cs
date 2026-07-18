// Strongly-typed IDs using the StronglyTypedId source generator.
// Each struct wraps a Guid — the compiler rejects mixing up StudentId with GroupId.
// JSON serialization, EF Core value converters, and OpenAPI schema filters are generated automatically.

using StronglyTypedIds;

namespace Domain.Ids;

[StronglyTypedId] public partial struct GroupJoinRequestId;
[StronglyTypedId] public partial struct ProcessManagerId;
[StronglyTypedId] public partial struct StudentId;
[StronglyTypedId] public partial struct GroupId;
[StronglyTypedId] public partial struct InstructorId;
[StronglyTypedId] public partial struct PaymentId;
