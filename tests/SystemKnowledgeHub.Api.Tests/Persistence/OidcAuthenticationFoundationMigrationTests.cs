using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class OidcAuthenticationFoundationMigrationTests
{
    [Fact]
    public async Task Existing_user_is_preserved_and_defaults_to_viewer_after_sec01_migration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"system-knowledge-hub-sec01-migration-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddDbContext<KnowledgeHubDbContext>(options =>
                options.UseSqlite($"Data Source={path};Foreign Keys=True;Pooling=False"));
            await using (var provider = services.BuildServiceProvider())
            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
                var migrator = dbContext.GetService<IMigrator>();

                await migrator.MigrateAsync("20260821221206_AddHumanConfirmationCurrentUserSnapshot");
                await dbContext.Database.OpenConnectionAsync();
                await using (var command = dbContext.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = """
                        INSERT INTO users (employee_no, display_name, email, department_or_team, job_title, is_active, created_at, updated_at, version)
                        VALUES ('SEC01-LEGACY', 'SEC-01 历史用户', 'legacy-sec01@example.test', '历史团队', '历史职位', 1, '2026-08-22T00:00:00+00:00', '2026-08-22T00:00:00+00:00', 1);
                        """;
                    await command.ExecuteNonQueryAsync();
                }
                await dbContext.Database.CloseConnectionAsync();

                await migrator.MigrateAsync();
                await dbContext.Database.OpenConnectionAsync();
                await using var verify = dbContext.Database.GetDbConnection().CreateCommand();
                verify.CommandText = "SELECT id, access_level FROM users WHERE employee_no = 'SEC01-LEGACY';";
                await using var reader = await verify.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(1L, reader.GetInt64(0));
                Assert.Equal("Viewer", reader.GetString(1));
            }
        }
        finally
        {
            foreach (var temporaryPath in new[] { path, $"{path}-wal", $"{path}-shm" })
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
