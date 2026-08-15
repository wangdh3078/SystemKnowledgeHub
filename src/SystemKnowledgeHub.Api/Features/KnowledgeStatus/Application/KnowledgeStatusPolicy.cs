using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.StatusProgression.Application;

public sealed class KnowledgeStatusPolicy
{
    public KnowledgeStatusPolicyResult Validate(
        KnowledgeStatus currentStatus,
        KnowledgeStatus targetStatus,
        string? reason,
        bool hasEvidence,
        bool hasHumanConfirmation)
    {
        if (currentStatus == targetStatus)
        {
            return new KnowledgeStatusPolicyResult(
                false,
                KnowledgeStatusFailure.Conflict,
                $"当前知识状态已是“{Label(currentStatus)}”。",
                null);
        }

        if (currentStatus == KnowledgeStatus.Unknown && targetStatus == KnowledgeStatus.Confirmed)
        {
            return Violation(
                "未知状态不能直接标记为已确认，必须先标记为推断。",
                "InferredStatus");
        }

        if (currentStatus == KnowledgeStatus.Unknown && targetStatus == KnowledgeStatus.Inferred)
        {
            return hasEvidence
                ? KnowledgeStatusPolicyResult.Allowed
                : Violation(
                    "标记为推断前，至少需要一条与当前知识对象明确相关的有效证据。",
                    "Evidence");
        }

        if (currentStatus == KnowledgeStatus.Inferred && targetStatus == KnowledgeStatus.Confirmed)
        {
            return hasHumanConfirmation
                ? KnowledgeStatusPolicyResult.Allowed
                : Violation(
                    "标记为已确认前，至少需要一条确认人快照完整的人工确认证据。",
                    "HumanConfirmation");
        }

        var isRollback = currentStatus == KnowledgeStatus.Confirmed
            || (currentStatus == KnowledgeStatus.Inferred && targetStatus == KnowledgeStatus.Unknown);
        if (isRollback)
        {
            return string.IsNullOrWhiteSpace(reason)
                ? Violation("回退知识状态时必须填写原因。", "Reason")
                : KnowledgeStatusPolicyResult.Allowed;
        }

        return Violation("当前知识状态不允许执行该变更。", "Transition");
    }

    private static KnowledgeStatusPolicyResult Violation(string message, string missingRequirement)
    {
        return new KnowledgeStatusPolicyResult(
            false,
            KnowledgeStatusFailure.BusinessRuleViolation,
            message,
            missingRequirement);
    }

    private static string Label(KnowledgeStatus status)
    {
        return status switch
        {
            KnowledgeStatus.Unknown => "未知",
            KnowledgeStatus.Inferred => "推断",
            KnowledgeStatus.Confirmed => "已确认",
            _ => status.ToString(),
        };
    }
}

public sealed record KnowledgeStatusPolicyResult(
    bool IsAllowed,
    KnowledgeStatusFailure Failure,
    string? Message,
    string? MissingRequirement)
{
    public static readonly KnowledgeStatusPolicyResult Allowed = new(true, KnowledgeStatusFailure.None, null, null);
}
