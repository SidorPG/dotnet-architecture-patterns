namespace Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid     Id         { get; init; }
    public string   EventType  { get; init; } = string.Empty;  // AssemblyQualifiedName
    public string   Payload    { get; init; } = string.Empty;  // JSON-serialised domain event
    public DateTime CreatedAt  { get; init; }
    public DateTime? SentAt    { get; set; }
    public int      RetryCount { get; set; }
    public string?  Error      { get; set; }
}
