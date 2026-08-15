namespace SystemKnowledgeHub.Api.Features.Systems.Domain;

public sealed class SystemTechnologyTag
{
    public long SystemId { get; set; }
    public string Technology { get; set; } = string.Empty;

    public KnowledgeSystem System { get; set; } = null!;
}
