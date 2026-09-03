using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Portal.Application;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class PortalCompositionValidatorTests
{
    [Fact]
    public void Section_validation_accepts_30_and_five_bodies_but_rejects_31_and_six()
    {
        var accepted = Enumerable.Range(0, 30).Select(index => new PortalPageSection
        {
            Id = index + 1,
            Heading = $"Section {index}",
            SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
            ProjectionKind = index < 5
                ? PortalPageProjectionKind.KnowledgeDocumentBody
                : PortalPageProjectionKind.Summary,
            SortOrder = index,
        }).ToArray();
        Assert.Empty(PortalCompositionValidator.ValidateSections(PortalTargetType.KnowledgeDocument, accepted));

        var sixBodies = accepted.Take(5).Append(new PortalPageSection
        {
            Id = 31,
            Heading = "Sixth body",
            SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
            ProjectionKind = PortalPageProjectionKind.KnowledgeDocumentBody,
            SortOrder = 5,
        }).ToArray();
        Assert.Contains("sections",
            PortalCompositionValidator.ValidateSections(PortalTargetType.KnowledgeDocument, sixBodies).Keys);

        var thirtyOneSummaries = Enumerable.Range(0, 31).Select(index => new PortalPageSection
        {
            Id = index + 1,
            Heading = $"Summary {index}",
            SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
            ProjectionKind = PortalPageProjectionKind.Summary,
            SortOrder = index,
        }).ToArray();
        Assert.Contains("sections",
            PortalCompositionValidator.ValidateSections(PortalTargetType.KnowledgeDocument, thirtyOneSummaries).Keys);
    }

    [Theory]
    [InlineData(PortalPageProjectionKind.KnowledgeDocumentBody, PortalTargetType.System)]
    [InlineData(PortalPageProjectionKind.DatabaseStructure, PortalTargetType.Integration)]
    [InlineData(PortalPageProjectionKind.StructuredOverview, PortalTargetType.KnowledgeDocument)]
    public void Section_validation_rejects_incompatible_projection(
        PortalPageProjectionKind projection,
        PortalTargetType primaryTargetType)
    {
        var section = new PortalPageSection
        {
            Id = 1,
            Heading = "Invalid",
            SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
            ProjectionKind = projection,
            SortOrder = 0,
        };
        Assert.Contains("sections[0].projectionKind",
            PortalCompositionValidator.ValidateSections(primaryTargetType, [section]).Keys);
    }

    [Fact]
    public void Section_validation_enforces_source_reference_shapes_and_duplicate_order()
    {
        var valid = new[]
        {
            new PortalPageSection
            {
                Id = 1,
                Heading = "Primary",
                SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
                ProjectionKind = PortalPageProjectionKind.Summary,
                SortOrder = 0,
            },
            new PortalPageSection
            {
                Id = 2,
                Heading = "Explicit",
                SourceKind = PortalPageSectionSourceKind.ExplicitReference,
                ReferenceTargetType = PortalTargetType.Integration,
                ReferenceTargetId = 1,
                ProjectionKind = PortalPageProjectionKind.Summary,
                SortOrder = 1,
            },
            new PortalPageSection
            {
                Id = 3,
                Heading = "Derived",
                SourceKind = PortalPageSectionSourceKind.Derived,
                ProjectionKind = PortalPageProjectionKind.RelatedKnowledge,
                SortOrder = 2,
            },
        };
        Assert.Empty(PortalCompositionValidator.ValidateSections(PortalTargetType.System, valid));

        valid[0].ReferenceTargetType = PortalTargetType.System;
        valid[0].ReferenceTargetId = 1;
        valid[1].ReferenceTargetId = null;
        valid[2].ReferenceTargetType = PortalTargetType.System;
        valid[2].ReferenceTargetId = 1;
        valid[2].SortOrder = 1;
        var errors = PortalCompositionValidator.ValidateSections(PortalTargetType.System, valid);
        Assert.Contains("sections[0].reference", errors.Keys);
        Assert.Contains("sections[1].reference", errors.Keys);
        Assert.Contains("sections[2].reference", errors.Keys);
        Assert.Contains("sections.sortOrder", errors.Keys);
    }

    [Fact]
    public async Task Node_validation_rejects_cycle_and_resulting_depth_over_ten()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new KnowledgeHubDbContext(
            new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = 87001,
            DisplayName = "Portal validator",
            AccessLevel = AccessLevel.Administrator,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.Users.Add(user);
        var parentId = (long?)null;
        for (var depth = 1; depth <= 10; depth++)
        {
            var node = Node(87000 + depth, parentId, depth, user.Id, now);
            db.PortalPageNodes.Add(node);
            parentId = node.Id;
        }
        await db.SaveChangesAsync();
        var validator = new PortalCompositionValidator(db);

        var validAtTen = Node(87101, 87009, 0, user.Id, now);
        Assert.Empty(await validator.ValidateNodePlacementAsync(validAtTen, CancellationToken.None));

        var tooDeep = Node(87100, parentId, 0, user.Id, now);
        var depthErrors = await validator.ValidateNodePlacementAsync(tooDeep, CancellationToken.None);
        Assert.Contains("parentId", depthErrors.Keys);

        var cycleCandidate = Node(87001, 87010, 1, user.Id, now);
        var cycleErrors = await validator.ValidateNodePlacementAsync(cycleCandidate, CancellationToken.None);
        Assert.Contains("parentId", cycleErrors.Keys);

        var selfCandidate = Node(87102, 87102, 0, user.Id, now);
        var selfErrors = await validator.ValidateNodePlacementAsync(selfCandidate, CancellationToken.None);
        Assert.Contains("parentId", selfErrors.Keys);

        var subtreeRoot = Node(87200, null, 20, user.Id, now);
        var subtreeChild = Node(87201, subtreeRoot.Id, 0, user.Id, now);
        var subtreeGrandchild = Node(87202, subtreeChild.Id, 0, user.Id, now);
        db.PortalPageNodes.AddRange(subtreeRoot, subtreeChild, subtreeGrandchild);
        await db.SaveChangesAsync();
        subtreeRoot.ParentId = 87008;
        var subtreeMoveErrors = await validator.ValidateNodePlacementAsync(subtreeRoot, CancellationToken.None);
        Assert.Contains("parentId", subtreeMoveErrors.Keys);
    }

    private static PortalPageNode Node(long id, long? parentId, int sortOrder, long userId, DateTimeOffset now) => new()
    {
        Id = id,
        ParentId = parentId,
        Title = $"Node {id}",
        NodeKind = PortalPageNodeKind.Folder,
        SortOrder = sortOrder,
        IsPublished = false,
        CreatedAt = now,
        CreatedByUserId = userId,
        CreatedByDisplayName = "Portal validator",
        UpdatedAt = now,
        UpdatedByUserId = userId,
        UpdatedByDisplayName = "Portal validator",
        Version = 1,
    };
}
