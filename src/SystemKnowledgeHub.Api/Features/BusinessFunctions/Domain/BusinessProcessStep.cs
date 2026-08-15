namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;

public sealed class BusinessProcessStep
{
    public long Id { get; set; }
    public long BusinessFunctionId { get; set; }
    public BusinessFunction BusinessFunction { get; set; } = null!;
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
