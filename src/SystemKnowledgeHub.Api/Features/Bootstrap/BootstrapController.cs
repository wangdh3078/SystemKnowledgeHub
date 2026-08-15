using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Bootstrap;

[ApiController]
[Route("api/bootstrap")]
public sealed class BootstrapController(KnowledgeHubDbContext dbContext) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<BootstrapStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BootstrapStatusResponse>> GetStatus(
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        var foreignKeysEnabled = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken)) == 1;

        if (!foreignKeysEnabled)
        {
            throw new InvalidOperationException(
                "SQLite foreign key enforcement is not enabled.");
        }

        return Ok(new BootstrapStatusResponse("ok", "SQLite"));
    }
}

public sealed record BootstrapStatusResponse(
    string Status,
    string DatabaseProvider);
