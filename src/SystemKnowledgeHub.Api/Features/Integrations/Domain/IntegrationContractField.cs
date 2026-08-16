namespace SystemKnowledgeHub.Api.Features.Integrations.Domain;

public sealed class IntegrationContractField
{
    public long Id { get; set; }
    public long IntegrationId { get; set; }
    public Integration Integration { get; set; } = null!;
    public int Ordinal { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? DataType { get; set; }
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
    public string? SampleValue { get; set; }
}
