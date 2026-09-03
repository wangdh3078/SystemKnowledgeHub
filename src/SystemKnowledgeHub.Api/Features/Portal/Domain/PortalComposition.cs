namespace SystemKnowledgeHub.Api.Features.Portal.Domain;

public sealed class PortalPage
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public PortalTargetType PrimaryTargetType { get; set; }
    public long PrimaryTargetId { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public long? PublishedByUserId { get; set; }
    public string? PublishedByDisplayName { get; set; }
    public DateTimeOffset? UnpublishedAt { get; set; }
    public long? UnpublishedByUserId { get; set; }
    public string? UnpublishedByDisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long CreatedByUserId { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public long UpdatedByUserId { get; set; }
    public string UpdatedByDisplayName { get; set; } = string.Empty;
    public long Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long? DeletedByUserId { get; set; }
    public string? DeletedByDisplayName { get; set; }

    public ICollection<PortalPageNode> Nodes { get; set; } = [];
    public ICollection<PortalPageSection> Sections { get; set; } = [];
}

public sealed class PortalPageNode
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public PortalPageNode? Parent { get; set; }
    public ICollection<PortalPageNode> Children { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public PortalPageNodeKind NodeKind { get; set; }
    public long? PortalPageId { get; set; }
    public PortalPage? PortalPage { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public long? PublishedByUserId { get; set; }
    public string? PublishedByDisplayName { get; set; }
    public DateTimeOffset? UnpublishedAt { get; set; }
    public long? UnpublishedByUserId { get; set; }
    public string? UnpublishedByDisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long CreatedByUserId { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public long UpdatedByUserId { get; set; }
    public string UpdatedByDisplayName { get; set; } = string.Empty;
    public long Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long? DeletedByUserId { get; set; }
    public string? DeletedByDisplayName { get; set; }
}

public sealed class PortalPageSection
{
    public long Id { get; set; }
    public long PortalPageId { get; set; }
    public PortalPage PortalPage { get; set; } = null!;
    public string Heading { get; set; } = string.Empty;
    public PortalPageSectionSourceKind SourceKind { get; set; }
    public PortalTargetType? ReferenceTargetType { get; set; }
    public long? ReferenceTargetId { get; set; }
    public PortalPageProjectionKind ProjectionKind { get; set; }
    public int SortOrder { get; set; }
}

public enum PortalTargetType
{
    System,
    BusinessFunction,
    DatabaseObject,
    KnowledgeDocument,
    Integration,
}

public enum PortalPageNodeKind
{
    Folder,
    Page,
}

public enum PortalPageSectionSourceKind
{
    PrimaryTarget,
    ExplicitReference,
    Derived,
}

public enum PortalPageProjectionKind
{
    Summary,
    KnowledgeDocumentBody,
    StructuredOverview,
    DatabaseStructure,
    AttachmentList,
    TrustSummary,
    RelatedKnowledge,
    Traceability,
}
