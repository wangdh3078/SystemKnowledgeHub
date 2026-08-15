using System.Globalization;

namespace SystemKnowledgeHub.Api.Shared.Api;

public static class ApiIdParser
{
    public const long JavaScriptMaxSafeInteger = 9_007_199_254_740_991;

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

    public static bool IsSafePositive(long id)
    {
        return id >= 1 && id <= JavaScriptMaxSafeInteger;
    }
}
