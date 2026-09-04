using System.Text.Json.Serialization;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Portal.Application.Models;

public readonly record struct PortalTargetKey(PortalTargetType Type, long Id);

public sealed record PortalTargetIdentity(
    PortalTargetType Type,
    long Id,
    string Title,
    string? DocumentType = null,
    string? Lifecycle = null);

public abstract record PortalResolvedTarget(PortalTargetType Type, long Id, string Title, string? Summary);

public sealed record PortalResolvedSystem(
    long Id,
    string Title,
    string? Summary,
    string Name,
    string DisplayName,
    string SystemType,
    string Lifecycle)
    : PortalResolvedTarget(PortalTargetType.System, Id, Title, Summary);

public sealed record PortalResolvedBusinessFunction(
    long Id,
    string Title,
    string? Summary,
    string Name,
    string? DisplayName,
    string FunctionType,
    string SystemName,
    string? CallerSummary,
    string? InputDescription,
    string? OutputDescription)
    : PortalResolvedTarget(PortalTargetType.BusinessFunction, Id, Title, Summary);

public sealed record PortalResolvedDatabaseObject(
    long Id,
    string Title,
    string? Summary,
    string? DatabaseComment,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    long? EstimatedRows,
    string AccessMode,
    IReadOnlyList<string> BusinessKeyColumns,
    IReadOnlyList<PortalResolvedDatabaseColumn> Columns)
    : PortalResolvedTarget(PortalTargetType.DatabaseObject, Id, Title, Summary);

public sealed record PortalResolvedDatabaseColumn(
    int OrdinalPosition,
    string ColumnName,
    string DataType,
    bool IsNullable,
    string? DatabaseComment);

public sealed record PortalResolvedKnowledgeDocument(
    long Id,
    string Title,
    string? Summary,
    string DocumentType,
    string BodyMarkdown)
    : PortalResolvedTarget(PortalTargetType.KnowledgeDocument, Id, Title, Summary);

public sealed record PortalResolvedIntegration(
    long Id,
    string Title,
    string? Summary,
    string IntegrationType,
    string SourcePartyName,
    string TargetPartyName,
    string FlowDirection)
    : PortalResolvedTarget(PortalTargetType.Integration, Id, Title, Summary);

public sealed record PortalTreeResponse(IReadOnlyList<PortalTreeNodeResponse> Items, int Total);

public sealed record PortalTreeNodeResponse(
    long NodeId,
    long? ParentNodeId,
    string Title,
    PortalPageNodeKind NodeKind,
    long? PageId);

public sealed record PortalHomeResponse(
    string PortalName,
    IReadOnlyList<PortalHomeCategoryResponse> Categories,
    IReadOnlyList<PortalRecentPageResponse> RecentPages);

public sealed record PortalHomeCategoryResponse(
    long NodeId,
    string Title,
    PortalPageNodeKind NodeKind,
    long? PageId);

public sealed record PortalRecentPageResponse(
    long Id,
    string Title,
    PortalTargetIdentityResponse PrimaryTarget,
    IReadOnlyList<PortalBreadcrumbItemResponse> Breadcrumb,
    DateTimeOffset PublishedAt);

public sealed record PortalPageResponse(
    long Id,
    string Title,
    PortalTargetIdentityResponse PrimaryTarget,
    IReadOnlyList<PortalBreadcrumbItemResponse> Breadcrumb,
    IReadOnlyList<PortalPageSectionResponse> Sections);

public sealed record PortalTargetIdentityResponse(PortalTargetType Type, long Id, string Title);

public sealed record PortalBreadcrumbItemResponse(long NodeId, string Title);

public sealed record PortalPageSectionResponse(
    long Id,
    string Heading,
    PortalPageSectionSourceKind SourceKind,
    PortalPageProjectionKind ProjectionKind,
    PortalSectionContentResponse Content);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PortalSummaryContentResponse), "Summary")]
[JsonDerivedType(typeof(PortalKnowledgeDocumentBodyContentResponse), "KnowledgeDocumentBody")]
[JsonDerivedType(typeof(PortalSystemOverviewContentResponse), "SystemOverview")]
[JsonDerivedType(typeof(PortalBusinessFunctionOverviewContentResponse), "BusinessFunctionOverview")]
[JsonDerivedType(typeof(PortalDatabaseObjectOverviewContentResponse), "DatabaseObjectOverview")]
[JsonDerivedType(typeof(PortalIntegrationOverviewContentResponse), "IntegrationOverview")]
[JsonDerivedType(typeof(PortalDatabaseStructureContentResponse), "DatabaseStructure")]
[JsonDerivedType(typeof(PortalAttachmentListContentResponse), "AttachmentList")]
[JsonDerivedType(typeof(PortalTrustSummaryContentResponse), "TrustSummary")]
[JsonDerivedType(typeof(PortalRelatedKnowledgeContentResponse), "RelatedKnowledge")]
[JsonDerivedType(typeof(PortalTraceabilityContentResponse), "Traceability")]
public abstract record PortalSectionContentResponse;

public sealed record PortalSummaryContentResponse(
    PortalTargetType TargetType,
    long TargetId,
    string Title,
    string? Summary) : PortalSectionContentResponse;

public sealed record PortalKnowledgeDocumentBodyContentResponse(
    long DocumentId,
    string Title,
    string DocumentType,
    string BodyMarkdown,
    IReadOnlyList<long> ImageAttachmentIds) : PortalSectionContentResponse;

public sealed record PortalSystemOverviewContentResponse(
    long SystemId,
    string Name,
    string DisplayName,
    string SystemType,
    string Lifecycle,
    string? Purpose) : PortalSectionContentResponse;

public sealed record PortalBusinessFunctionOverviewContentResponse(
    long BusinessFunctionId,
    string Name,
    string? DisplayName,
    string FunctionType,
    string SystemName,
    string? Purpose,
    string? CallerSummary,
    string? InputDescription,
    string? OutputDescription) : PortalSectionContentResponse;

public sealed record PortalDatabaseObjectOverviewContentResponse(
    long DatabaseObjectId,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    string? BusinessDescription,
    string? DatabaseComment,
    long? EstimatedRows,
    string AccessMode,
    IReadOnlyList<string> BusinessKeyColumns) : PortalSectionContentResponse;

public sealed record PortalIntegrationOverviewContentResponse(
    long IntegrationId,
    string Name,
    string IntegrationType,
    string SourcePartyName,
    string TargetPartyName,
    string FlowDirection,
    string? Purpose) : PortalSectionContentResponse;

public sealed record PortalDatabaseStructureContentResponse(
    long DatabaseObjectId,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    string? BusinessDescription,
    string? DatabaseComment,
    long? EstimatedRows,
    string AccessMode,
    IReadOnlyList<string> BusinessKeyColumns,
    IReadOnlyList<PortalDatabaseColumnResponse> Columns) : PortalSectionContentResponse;

public sealed record PortalDatabaseColumnResponse(
    int Ordinal,
    string ColumnName,
    string NativeDataType,
    bool Nullable,
    string? DatabaseComment);

public sealed record PortalAttachmentListContentResponse(
    long DocumentId,
    IReadOnlyList<PortalAttachmentResponse> Attachments) : PortalSectionContentResponse;

public sealed record PortalAttachmentResponse(
    long AttachmentId,
    string DisplayName,
    string Kind,
    string ContentType,
    long SizeBytes,
    string PreviewMode,
    bool CanPreview,
    bool CanDownload);

public sealed record PortalAttachmentTextPreviewResponse(
    string Mode,
    string Text,
    bool Truncated,
    int ReturnedBytes,
    int MaximumBytes);

public sealed record PortalAttachmentCsvPreviewResponse(
    string Mode,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool Truncated,
    IReadOnlyList<string> TruncationReasons,
    int MaximumRows,
    int MaximumColumns,
    int MaximumCharacters);

public sealed record PortalAttachmentSpreadsheetPreviewResponse(
    string Mode,
    IReadOnlyList<string> Sheets,
    string SelectedSheet,
    IReadOnlyList<PortalAttachmentSpreadsheetRowResponse> Rows,
    bool Truncated,
    IReadOnlyList<string> TruncationReasons,
    int MaximumSheets,
    int MaximumRows,
    int MaximumColumns);

public sealed record PortalAttachmentSpreadsheetRowResponse(int RowNumber, IReadOnlyList<string> Cells);

public sealed record PortalTrustSummaryContentResponse(
    PortalTargetType TargetType,
    string TargetTitle,
    KnowledgeStatus KnowledgeStatus,
    int EvidenceCount,
    int HumanConfirmationCount,
    string? ConfirmationCoverage) : PortalSectionContentResponse;

public sealed record PortalRelatedKnowledgeContentResponse(
    IReadOnlyList<PortalRelatedKnowledgeGroupResponse> Groups) : PortalSectionContentResponse;

public sealed record PortalRelatedKnowledgeGroupResponse(
    RelationType RelationType,
    string RelationLabel,
    string Direction,
    IReadOnlyList<PortalRelatedKnowledgeItemResponse> Items);

public sealed record PortalRelatedKnowledgeItemResponse(
    PortalTargetType TargetType,
    string TargetTitle,
    KnowledgeStatus KnowledgeStatus,
    int EvidenceCount,
    int HumanConfirmationCount,
    KnowledgeStatus RelationKnowledgeStatus,
    int RelationEvidenceCount,
    int RelationHumanConfirmationCount,
    long? PortalPageId);

public sealed record PortalTraceabilityContentResponse(
    PortalTraceNodeResponse Root,
    IReadOnlyList<PortalTracePathResponse> Paths,
    IReadOnlyList<string> MissingLinkCodes,
    bool CycleDetected,
    bool IsTruncated,
    PortalTraceLimitsResponse Limits) : PortalSectionContentResponse;

public sealed record PortalTracePathResponse(
    string Kind,
    IReadOnlyList<PortalTraceNodeResponse> Nodes,
    IReadOnlyList<PortalTraceEdgeResponse> Edges);

public sealed record PortalTraceNodeResponse(
    DocumentType DocumentType,
    string Title,
    KnowledgeStatus KnowledgeStatus,
    int EvidenceCount,
    int HumanConfirmationCount,
    string ConfirmationCoverage,
    long? PortalPageId);

public sealed record PortalTraceEdgeResponse(
    RelationType RelationType,
    KnowledgeStatus KnowledgeStatus,
    int EvidenceCount,
    int HumanConfirmationCount);

public sealed record PortalTraceLimitsResponse(int MaxDepth, int MaxNodes, int MaxEdges);

public sealed record PortalSearchResponse(
    IReadOnlyList<PortalSearchItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record PortalSearchItemResponse(
    long PageId,
    string Title,
    PortalTargetType PrimaryTargetType,
    string PrimaryTargetTitle,
    IReadOnlyList<PortalBreadcrumbItemResponse> Breadcrumb,
    string Snippet);

public enum PortalReadFailure
{
    None,
    NotFound,
    LimitExceeded,
}

public sealed record PortalTreeResult(
    PortalReadFailure Failure,
    PortalTreeResponse? Response = null);

public sealed record PortalHomeResult(
    PortalReadFailure Failure,
    PortalHomeResponse? Response = null);

public sealed record PortalPageResult(
    PortalReadFailure Failure,
    PortalPageResponse? Response = null);

public sealed record PortalSearchResult(
    PortalReadFailure Failure,
    PortalSearchResponse? Response = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);
