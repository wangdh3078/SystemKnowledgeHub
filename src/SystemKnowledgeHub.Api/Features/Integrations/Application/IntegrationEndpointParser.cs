using System.Text.Json;
using System.Text.Json.Nodes;
using SystemKnowledgeHub.Api.Features.Integrations.Application.Models;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;

namespace SystemKnowledgeHub.Api.Features.Integrations.Application;

public static class IntegrationEndpointParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(IntegrationType type, JsonElement? element, out IntegrationEndpoint endpoint, out string? display, out string? error)
    {
        endpoint = new(null, null, null, null, null, null); display = null; error = null;
        string? parseError = null;
        if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            error = type == IntegrationType.DatabaseDependency ? null : "必须填写该集成类型的 Endpoint 信息。";
            return error is null;
        }
        if (element.Value.ValueKind != JsonValueKind.Object) { error = "Endpoint 必须是对象。"; return false; }
        var map = element.Value.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        string? Read(string name)
        {
            if (!map.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind != JsonValueKind.String) { parseError = $"endpoint.{name} 必须是字符串。"; return null; }
            return string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()!.Trim();
        }
        bool Only(params string[] names)
        {
            var invalid = map.Keys.FirstOrDefault(key => !names.Contains(key, StringComparer.Ordinal));
            if (invalid is null) return true;
            parseError = $"endpoint 不支持字段 {invalid}。"; return false;
        }
        switch (type)
        {
            case IntegrationType.HttpApi:
                if (!Only("url", "method")) { error = parseError; return false; }
                var url = Read("url"); var method = Read("method");
                if (parseError is not null) { error = parseError; return false; }
                if (url is null) { error = "HTTP API 必须填写 endpoint.url。"; return false; }
                endpoint = new(url, method, null, null, null, null); display = method is null ? url : $"{method} {url}"; return true;
            case IntegrationType.RabbitMq:
                if (!Only("exchange", "topic", "queue")) { error = parseError; return false; }
                var exchange = Read("exchange"); var topic = Read("topic"); var queue = Read("queue");
                if (parseError is not null) { error = parseError; return false; }
                if (topic is null && queue is null) { error = "RabbitMQ 必须填写 endpoint.topic 或 endpoint.queue。"; return false; }
                endpoint = new(null, null, exchange, topic, queue, null); display = topic ?? queue; return true;
            case IntegrationType.FileExchange:
                if (!Only("filePath")) { error = parseError; return false; }
                var filePath = Read("filePath"); if (parseError is not null) { error = parseError; return false; }
                if (filePath is null) { error = "文件交换必须填写 endpoint.filePath。"; return false; }
                endpoint = new(null, null, null, null, null, filePath); display = filePath; return true;
            case IntegrationType.DatabaseDependency:
                if (!Only()) { error = parseError; return false; }
                return true;
            default: error = "IntegrationType 无效。"; return false;
        }
    }

    public static string? Serialize(IntegrationEndpoint endpoint, IntegrationType type) => type switch
    {
        IntegrationType.HttpApi => JsonSerializer.Serialize(new { url = endpoint.Url, method = endpoint.Method }, JsonOptions),
        IntegrationType.RabbitMq => JsonSerializer.Serialize(new { exchange = endpoint.Exchange, topic = endpoint.Topic, queue = endpoint.Queue }, JsonOptions),
        IntegrationType.FileExchange => JsonSerializer.Serialize(new { filePath = endpoint.FilePath }, JsonOptions),
        IntegrationType.DatabaseDependency => null,
        _ => null,
    };
    public static IntegrationEndpointResponse Deserialize(string? json) => string.IsNullOrWhiteSpace(json)
        ? new(null, null, null, null, null, null)
        : JsonSerializer.Deserialize<IntegrationEndpointResponse>(json, JsonOptions) ?? new(null, null, null, null, null, null);
}
