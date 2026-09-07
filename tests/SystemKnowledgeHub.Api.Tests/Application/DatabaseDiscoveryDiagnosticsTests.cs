using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class DatabaseDiscoveryDiagnosticsTests
{
    [Fact]
    public void Every_closed_stage_emits_only_safe_fields_and_preserves_internal_cause()
    {
        foreach (var stage in Enum.GetValues<DiscoveryFailureStage>())
        {
            const string canary = "PASSWORD_CANARY_DO_NOT_LOG SERVER_RAW_ERROR_CANARY SELECT_SECRET_CANARY";
            var cause = new InvalidOperationException(canary);
            var wrapper = new DatabaseDiscoveryProviderException("MetadataQueryFailed", canary, canary, cause);
            Assert.Same(cause, wrapper.InnerException);
            var logger = new SafeDiagnosticLogger<DatabaseDiscoveryDiagnosticsTests>();
            DatabaseDiscoveryDiagnostics.Log(logger, wrapper, stage, wrapper.ErrorCode, 42, 7, DatabaseProviderType.Oracle);
            var log = Assert.Single(logger.Entries);
            Assert.Contains("Stage=" + stage, log);
            Assert.Contains("RunId=42", log);
            Assert.Contains("ProfileId=7", log);
            Assert.Contains("UnexpectedProgramFailure", log);
            Assert.Contains("System.InvalidOperationException", log);
            Assert.Contains("HResult=", log);
            Assert.DoesNotContain(canary, log);
        }
    }
}
