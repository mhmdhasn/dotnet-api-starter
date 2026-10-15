namespace StarterKit.Api.Domain;

/// <summary>
/// Base class for all domain entities. <see cref="CreatedAt"/> and <see cref="UpdatedAt"/> are filled in
/// automatically by <c>AuditSaveChangesInterceptor</c>; the Id is a time-ordered GUID (v7) generated client-side.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
