using System.Globalization;

namespace SystemKnowledgeHub.Api.Shared.Api;

/// <summary>
/// 验证并转换会暴露给 JavaScript 调用方的 API 标识符。
/// </summary>
/// <remarks>
/// 此工具只验证数值格式和安全范围。验证失败表示请求中的 ID 无效，
/// 不表示对应业务资源不存在。
/// </remarks>
public static class ApiIdParser
{
    /// <summary>
    /// JavaScript 可以精确表示的最大整数 API 标识符。
    /// </summary>
    public const long JavaScriptMaxSafeInteger = 9_007_199_254_740_991;

    /// <summary>
    /// 将原始 API 标识符解析为 JavaScript 安全的正整数。
    /// </summary>
    /// <param name="value">来自路由或查询参数的原始文本；<see langword="null"/> 视为无效。</param>
    /// <param name="id">当方法返回 <see langword="true"/> 时，解析得到的安全正整数。</param>
    /// <returns>
    /// 当 <paramref name="value"/> 是 1 到 <see cref="JavaScriptMaxSafeInteger"/> 之间的十进制整数时为
    /// <see langword="true"/>；格式错误、零、负数或超出安全范围时为 <see langword="false"/>。
    /// </returns>
    public static bool TryParse(string? value, out long id)
    {
        return long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out id)
            && id >= 1
            && id <= JavaScriptMaxSafeInteger;
    }

    /// <summary>
    /// 判断已解析的标识符是否为 JavaScript 安全的正整数。
    /// </summary>
    /// <param name="id">待检查的标识符。</param>
    /// <returns>当 <paramref name="id"/> 位于允许范围内时为 <see langword="true"/>。</returns>
    public static bool IsSafePositive(long id)
    {
        return id >= 1 && id <= JavaScriptMaxSafeInteger;
    }
}
