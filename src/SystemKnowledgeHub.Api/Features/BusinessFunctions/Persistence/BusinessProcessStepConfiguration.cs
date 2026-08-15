using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Persistence;

public sealed class BusinessProcessStepConfiguration : IEntityTypeConfiguration<BusinessProcessStep>
{
    public void Configure(EntityTypeBuilder<BusinessProcessStep> builder)
    {
        builder.ToTable("business_process_steps", table =>
            table.HasCheckConstraint("ck_business_process_steps_order", "step_order > 0"));

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(entity => entity.BusinessFunctionId).HasColumnName("business_function_id").IsRequired();
        builder.Property(entity => entity.StepOrder).HasColumnName("step_order").IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("name").IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("description");

        builder.HasOne(entity => entity.BusinessFunction)
            .WithMany(function => function.ProcessSteps)
            .HasForeignKey(entity => entity.BusinessFunctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.BusinessFunctionId, entity.StepOrder }).IsUnique();
    }
}
