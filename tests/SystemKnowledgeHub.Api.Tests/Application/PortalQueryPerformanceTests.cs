using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SystemKnowledgeHub.Api.Features.Portal.Application;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class PortalQueryPerformanceTests
{
    [Fact]
    public async Task Page_read_query_count_is_fixed_when_sections_grow_from_one_to_thirty()
    {
        var counter = new CommandCounter();
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;
        await using var db = new KnowledgeHubDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = "Portal performance actor",
            AccessLevel = AccessLevel.Administrator,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var system = new KnowledgeSystem
        {
            Name = "portal-performance-system",
            DisplayName = "Portal performance system",
            SystemType = "Application",
            Lifecycle = SystemLifecycle.Running,
            Purpose = "Purpose",
            CreatedAt = now,
            CreatedByUserId = user.Id,
            CreatedByName = user.DisplayName,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = user.DisplayName,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        db.Systems.Add(system);
        await db.SaveChangesAsync();
        var oneSectionPage = Page("One section", system.Id, user.Id, now, 1);
        var thirtySectionPage = Page("Thirty sections", system.Id, user.Id, now, 30);
        db.PortalPages.AddRange(oneSectionPage, thirtySectionPage);
        await db.SaveChangesAsync();
        var root = Node("Root", PortalPageNodeKind.Folder, null, null, 0, user.Id, now);
        db.PortalPageNodes.Add(root);
        await db.SaveChangesAsync();
        db.PortalPageNodes.AddRange(
            Node("One", PortalPageNodeKind.Page, root.Id, oneSectionPage.Id, 0, user.Id, now),
            Node("Thirty", PortalPageNodeKind.Page, root.Id, thirtySectionPage.Id, 1, user.Id, now));
        await db.SaveChangesAsync();
        var resolver = new PortalTargetResolver(db);
        var queries = new PortalQueries(db, resolver, NullLogger<PortalQueries>.Instance);

        counter.Reset();
        var oneResult = await queries.GetPageAsync(oneSectionPage.Id, CancellationToken.None);
        var oneCount = counter.Count;
        counter.Reset();
        var thirtyResult = await queries.GetPageAsync(thirtySectionPage.Id, CancellationToken.None);
        var thirtyCount = counter.Count;

        Assert.Equal(PortalReadFailure.None, oneResult.Failure);
        Assert.Equal(PortalReadFailure.None, thirtyResult.Failure);
        Assert.Equal(oneCount, thirtyCount);
        Assert.InRange(thirtyCount, 1, 25);
    }

    private static PortalPage Page(
        string title,
        long systemId,
        long userId,
        DateTimeOffset now,
        int sectionCount)
    {
        var page = new PortalPage
        {
            Title = title,
            PrimaryTargetType = PortalTargetType.System,
            PrimaryTargetId = systemId,
            IsPublished = true,
            PublishedAt = now,
            PublishedByUserId = userId,
            PublishedByDisplayName = "Portal performance actor",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByDisplayName = "Portal performance actor",
            UpdatedAt = now,
            UpdatedByUserId = userId,
            UpdatedByDisplayName = "Portal performance actor",
            Version = 1,
        };
        page.Sections = Enumerable.Range(0, sectionCount).Select(index => new PortalPageSection
        {
            PortalPage = page,
            Heading = $"Summary {index}",
            SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
            ProjectionKind = PortalPageProjectionKind.Summary,
            SortOrder = index,
        }).ToArray();
        return page;
    }

    private static PortalPageNode Node(
        string title,
        PortalPageNodeKind kind,
        long? parentId,
        long? pageId,
        int sortOrder,
        long userId,
        DateTimeOffset now) => new()
        {
            Title = title,
            NodeKind = kind,
            ParentId = parentId,
            PortalPageId = pageId,
            SortOrder = sortOrder,
            IsPublished = true,
            PublishedAt = now,
            PublishedByUserId = userId,
            PublishedByDisplayName = "Portal performance actor",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByDisplayName = "Portal performance actor",
            UpdatedAt = now,
            UpdatedByUserId = userId,
            UpdatedByDisplayName = "Portal performance actor",
            Version = 1,
        };

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public void Reset() => Count = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Count++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
