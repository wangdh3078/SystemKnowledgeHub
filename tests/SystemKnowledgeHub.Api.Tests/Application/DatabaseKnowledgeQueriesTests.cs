using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class DatabaseKnowledgeQueriesTests
    : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public DatabaseKnowledgeQueriesTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDatabaseObjectDetail_projects_page_contract_and_selected_column()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<DatabaseKnowledgeQueries>();

        var result = await queries.GetDatabaseObjectDetail(45, 123, CancellationToken.None);

        Assert.False(result.SelectedColumnInvalid);
        Assert.NotNull(result.Detail);
        Assert.Equal("MES.TABLE_EQP", result.Detail.Overview.QualifiedName);
        Assert.Equal("Inferred", result.Detail.Overview.KnowledgeStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail.ConcurrencyToken));
        Assert.Equal(8, result.Detail.Columns.Count);
        Assert.Equal(123, result.Detail.Columns.Single(column => column.Selected).Id);
        Assert.Equal(123, result.Detail.SelectedColumnDrawer?.ColumnId);
    }

    [Fact]
    public async Task GetDatabaseObjectDetail_distinguishes_missing_object_and_invalid_selected_column()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<DatabaseKnowledgeQueries>();

        var missing = await queries.GetDatabaseObjectDetail(999, null, CancellationToken.None);
        var invalidSelection = await queries.GetDatabaseObjectDetail(45, 999, CancellationToken.None);

        Assert.Null(missing.Detail);
        Assert.False(missing.SelectedColumnInvalid);
        Assert.Null(invalidSelection.Detail);
        Assert.True(invalidSelection.SelectedColumnInvalid);
    }

    [Fact]
    public async Task GetColumnDetail_projects_known_values_status_and_concurrency_token()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<DatabaseKnowledgeQueries>();

        var detail = await queries.GetColumnDetail(123, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("STATE_FLAG", detail.DatabaseMetadata.ColumnName);
        Assert.Equal("Inferred", detail.BusinessKnowledge.KnowledgeStatus);
        Assert.Equal(3, detail.KnownValues.Count);
        Assert.False(string.IsNullOrWhiteSpace(detail.ConcurrencyToken));
        Assert.Empty(detail.Evidence);
        Assert.Empty(detail.Relations);
        Assert.Empty(detail.UnknownItems);
    }

    [Fact]
    public async Task GetColumnDetail_returns_null_for_missing_resource()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<DatabaseKnowledgeQueries>();

        Assert.Null(await queries.GetColumnDetail(999, CancellationToken.None));
    }
}
