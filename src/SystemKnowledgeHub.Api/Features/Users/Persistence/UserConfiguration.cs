using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using UserEntity = SystemKnowledgeHub.Api.Features.Users.Domain.User;

namespace SystemKnowledgeHub.Api.Features.Users.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_is_active", "is_active IN (0,1)");
            table.HasCheckConstraint("ck_users_version", "version >= 1");
            table.HasCheckConstraint("ck_users_access_level", "access_level IN ('Viewer','Editor','Administrator')");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.EmployeeNo).HasColumnName("employee_no").UseCollation("NOCASE");
        builder.Property(entity => entity.DisplayName).HasColumnName("display_name").UseCollation("NOCASE").IsRequired();
        builder.Property(entity => entity.Email).HasColumnName("email").UseCollation("NOCASE");
        builder.Property(entity => entity.DepartmentOrTeam).HasColumnName("department_or_team");
        builder.Property(entity => entity.JobTitle).HasColumnName("job_title");
        builder.Property(entity => entity.AccessLevel)
            .HasColumnName("access_level")
            .HasConversion<string>()
            .HasDefaultValue(AccessLevel.Viewer)
            .IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(entity => entity.EmployeeNo)
            .IsUnique()
            .HasFilter("employee_no IS NOT NULL");
        builder.HasIndex(entity => entity.Email)
            .IsUnique()
            .HasFilter("email IS NOT NULL");
        builder.HasIndex(entity => new { entity.IsActive, entity.DisplayName });
    }
}
