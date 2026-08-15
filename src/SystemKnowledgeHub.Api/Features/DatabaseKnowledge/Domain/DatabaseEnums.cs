namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

public enum DatabaseObjectType
{
    Table,
    View,
}

public enum DatabaseAccessMode
{
    Read,
    Write,
    ReadWrite,
    Unknown,
}
