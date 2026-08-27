using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class DatabaseKnowledgeMappingTests
    : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public DatabaseKnowledgeMappingTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Slice_tables_use_frozen_names_constraints_and_restrict_foreign_keys()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();

        Assert.Equal("database_sources", dbContext.Model.FindEntityType(typeof(DatabaseSource))?.GetTableName());
        Assert.Equal("database_objects", dbContext.Model.FindEntityType(typeof(DatabaseObject))?.GetTableName());
        Assert.Equal("database_columns", dbContext.Model.FindEntityType(typeof(DatabaseColumn))?.GetTableName());
        Assert.Equal("column_known_values", dbContext.Model.FindEntityType(typeof(ColumnKnownValue))?.GetTableName());

        var objectForeignKey = dbContext.Model.FindEntityType(typeof(DatabaseObject))!
            .GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(DatabaseSource));
        var columnForeignKey = dbContext.Model.FindEntityType(typeof(DatabaseColumn))!
            .GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(DatabaseObject));
        var knownValueForeignKey = dbContext.Model.FindEntityType(typeof(ColumnKnownValue))!
            .GetForeignKeys()
            .Single();

        Assert.Equal(DeleteBehavior.Restrict, objectForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, columnForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, knownValueForeignKey.DeleteBehavior);

        var columnModel = dbContext.Model.FindEntityType(typeof(DatabaseColumn))!;
        Assert.True(columnModel.FindProperty(nameof(DatabaseColumn.BusinessDescription))!.IsNullable);
        Assert.True(columnModel.FindProperty(nameof(DatabaseColumn.DefaultValue))!.IsNullable);
        Assert.True(columnModel.FindProperty(nameof(DatabaseColumn.Version))!.IsConcurrencyToken);
    }

    [Fact]
    public async Task Known_value_unique_constraint_is_enforced_by_real_sqlite()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        dbContext.ColumnKnownValues.Add(new ColumnKnownValue
        {
            DatabaseColumnId = 123,
            ValueText = "30",
            Meaning = "重复值",
            SortOrder = 31,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}
