namespace SystemKnowledgeHub.Api.Shared.Api.Contracts;

/// <summary>
/// 所有失败 HTTP 响应使用的统一错误 envelope。
/// </summary>
/// <param name="Code">稳定的机器可读错误类别，调用方可据此区分当前 API 定义的失败情形。</param>
/// <param name="Message">面向调用方的错误说明；字段级问题应同时查看 <paramref name="FieldErrors"/>。</param>
/// <param name="FieldErrors">按请求字段名分组的验证错误；没有字段级验证错误时为 <see langword="null"/>。</param>
/// <param name="Details">与该错误类别相关的附加结构化上下文；没有附加上下文时为 <see langword="null"/>。</param>
public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    object? Details);
