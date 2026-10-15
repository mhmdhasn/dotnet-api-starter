using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Api.Domain;

namespace StarterKit.Api.Persistence.Configurations;

/// <summary>
/// Inherit from this for every entity that derives from <see cref="BaseEntity"/>; it applies the shared mapping
/// and calls <see cref="ConfigureEntity"/> for the entity-specific part. Picked up by ApplyConfigurationsFromAssembly.
/// </summary>
public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CreatedAt).IsRequired();

        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
