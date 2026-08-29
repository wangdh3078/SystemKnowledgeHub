namespace SystemKnowledgeHub.Api.Features.Attachments.Domain;

public sealed class Attachment
{
    public long Id { get; set; }
    public long KnowledgeDocumentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public AttachmentKind Kind { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public byte[] Sha256 { get; set; } = [];
    public AttachmentStorageState StorageState { get; set; }
    public long CreatedByUserId { get; set; }
    public string CreatedByDisplayNameSnapshot { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public long Version { get; set; } = 1;
}

public enum AttachmentKind
{
    Image,
    File,
}

public enum AttachmentStorageState
{
    Ready,
    DeletePending,
}
