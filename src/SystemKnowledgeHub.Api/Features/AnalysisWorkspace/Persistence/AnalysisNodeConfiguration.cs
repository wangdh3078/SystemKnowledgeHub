using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;

namespace SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Persistence;

public sealed class AnalysisNodeConfiguration : IEntityTypeConfiguration<AnalysisNode>
{
    public void Configure(EntityTypeBuilder<AnalysisNode> builder)
    {
        builder.ToTable("analysis_nodes", table =>
        {
            table.HasCheckConstraint("ck_analysis_nodes_id", "id BETWEEN 1 AND 9007199254740991");
            table.HasCheckConstraint("ck_analysis_nodes_type", "node_type IN ('Folder','Document')");
            table.HasCheckConstraint("ck_analysis_nodes_shape", "(node_type = 'Folder' AND title IS NOT NULL AND length(trim(title)) BETWEEN 1 AND 200 AND title = trim(title) AND knowledge_document_id IS NULL) OR (node_type = 'Document' AND title IS NULL AND knowledge_document_id IS NOT NULL)");
            table.HasCheckConstraint("ck_analysis_nodes_parent", "parent_id IS NULL OR parent_id <> id");
            table.HasCheckConstraint("ck_analysis_nodes_order", "sort_order BETWEEN 0 AND 2147483647");
            table.HasCheckConstraint("ck_analysis_nodes_version", "version >= 1");
        });
        builder.HasKey(node => node.Id);
        builder.Property(node => node.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(node => node.ParentId).HasColumnName("parent_id");
        builder.Property(node => node.NodeType).HasColumnName("node_type").HasConversion<string>().IsRequired();
        builder.Property(node => node.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(node => node.KnowledgeDocumentId).HasColumnName("knowledge_document_id");
        builder.Property(node => node.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(node => node.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(node => node.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(node => node.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasOne<AnalysisNode>().WithMany().HasForeignKey(node => node.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(node => node.KnowledgeDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(node => node.SortOrder).IsUnique().HasFilter("parent_id IS NULL")
            .HasDatabaseName("IX_analysis_nodes_root_order");
        builder.HasIndex(node => new { node.ParentId, node.SortOrder }).IsUnique().HasFilter("parent_id IS NOT NULL")
            .HasDatabaseName("IX_analysis_nodes_parent_order");
        builder.HasIndex(node => node.KnowledgeDocumentId).IsUnique().HasFilter("knowledge_document_id IS NOT NULL")
            .HasDatabaseName("IX_analysis_nodes_document");
    }
}
