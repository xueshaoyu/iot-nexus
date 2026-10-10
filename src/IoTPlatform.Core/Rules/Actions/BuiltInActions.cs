using System.Net.Http.Json;
using System.Text;
using IoTPlatform.Data;
using IoTPlatform.Services;
using NewLife;
using NewLife.Log;

namespace IoTPlatform.Rules.Actions;

/// <summary>产生告警</summary>
public class AlarmAction(AlarmService alarmService) : IRuleAction
{
    public String Name => "alarm";
    public String DisplayName => "Raise alarm";
    public String Description => "Create an alarm record (merged while an active alarm exists for the same rule and device).";

    public IReadOnlyList<RuleActionParameter> Parameters { get; } =
    [
        new("level", "Level", "Info / Warning / Critical", false, "Warning", ["Info", "Warning", "Critical"]),
        new("title", "Title", "Supports templates such as {rule.name}", false, "{rule.name}"),
        new("content", "Content", "Supports templates such as {device.name} {temperature}", false, "{device.name} triggered {rule.name}"),
    ];

    public Task<RuleActionResult> ExecuteAsync(Rule rule, RuleContext context, IDictionary<String, String?> parameters, CancellationToken cancellationToken)
    {
        var level = Enum.TryParse<AlarmLevels>(parameters.GetValueOrDefault("level"), true, out var l) ? l : AlarmLevels.Warning;
        var title = parameters.GetValueOrDefault("title");
        var alarm = alarmService.Raise(rule.Id, context.Device, level, title.IsNullOrEmpty() ? rule.Name : title, parameters.GetValueOrDefault("content"));

        return Task.FromResult(RuleActionResult.Ok($"alarm #{alarm.Id}"));
    }
}

/// <summary>调用 Webhook</summary>
public class WebhookAction(IHttpClientFactory httpClientFactory) : IRuleAction
{
    public String Name => "webhook";
    public String DisplayName => "Webhook";
    public String Description => "POST a JSON payload to an HTTP endpoint. Leave body empty to send the default payload.";

    public IReadOnlyList<RuleActionParameter> Parameters { get; } =
    [
        new("url", "URL", "http(s) endpoint", true),
        new("body", "Body", "Optional JSON template, e.g. {\"text\":\"{device.name} {temperature}\"}"),
    ];

    public async Task<RuleActionResult> ExecuteAsync(Rule rule, RuleContext context, IDictionary<String, String?> parameters, CancellationToken cancellationToken)
    {
        var url = parameters.GetValueOrDefault("url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return RuleActionResult.Fail("invalid url");

        var client = httpClientFactory.CreateClient(nameof(WebhookAction));
        var body = parameters.GetValueOrDefault("body");
        using HttpContent content = body.IsNullOrWhiteSpace()
            ? JsonContent.Create(new
            {
                rule = new { rule.Id, rule.Name },
                device = new { context.Device.Id, context.Device.Code, context.Device.Name },
                product = context.Product?.Code,
                trigger = context.TriggerType,
                eventName = context.EventName,
                values = context.Values,
                time = context.Time,
            }, options: JsonDefaults.Options)
            : new StringContent(body!, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(uri, content, cancellationToken);
        return response.IsSuccessStatusCode
            ? RuleActionResult.Ok($"HTTP {(Int32)response.StatusCode}")
            : RuleActionResult.Fail($"HTTP {(Int32)response.StatusCode}");
    }
}

/// <summary>设置设备属性（联动控制）</summary>
public class SetPropertyAction(DeviceCommandService commandService) : IRuleAction
{
    public String Name => "set-property";
    public String DisplayName => "Set device property";
    public String Description => "Write a read-write property on the triggering device or another device (device linkage).";

    public IReadOnlyList<RuleActionParameter> Parameters { get; } =
    [
        new("device", "Target device code", "Empty = the triggering device"),
        new("property", "Property", "Identifier of a read-write property", true),
        new("value", "Value", "Supports templates", true),
    ];

    public async Task<RuleActionResult> ExecuteAsync(Rule rule, RuleContext context, IDictionary<String, String?> parameters, CancellationToken cancellationToken)
    {
        var code = parameters.GetValueOrDefault("device");
        var device = code.IsNullOrWhiteSpace() ? context.Device : Device.FindByCode(code);
        if (device == null) return RuleActionResult.Fail($"device '{code}' not found");

        var values = new Dictionary<String, Object?> { [parameters["property"]!] = parameters.GetValueOrDefault("value") };
        var record = await commandService.SetPropertiesAsync(device, values, $"Rule:{rule.Name}", 0, cancellationToken);

        return record.Status == ServiceStatusKinds.Failed ? RuleActionResult.Fail(record.OutputData ?? "failed") : RuleActionResult.Ok($"{device.Code} {record.Status}");
    }
}

/// <summary>调用设备服务</summary>
public class InvokeServiceAction(DeviceCommandService commandService) : IRuleAction
{
    public String Name => "invoke-service";
    public String DisplayName => "Invoke device service";
    public String Description => "Call a service defined in the thing model on the triggering device or another device.";

    public IReadOnlyList<RuleActionParameter> Parameters { get; } =
    [
        new("device", "Target device code", "Empty = the triggering device"),
        new("service", "Service", "Service identifier", true),
        new("args", "Arguments (JSON)", "e.g. {\"level\":1}", false, "{}"),
    ];

    public async Task<RuleActionResult> ExecuteAsync(Rule rule, RuleContext context, IDictionary<String, String?> parameters, CancellationToken cancellationToken)
    {
        var code = parameters.GetValueOrDefault("device");
        var device = code.IsNullOrWhiteSpace() ? context.Device : Device.FindByCode(code);
        if (device == null) return RuleActionResult.Fail($"device '{code}' not found");

        var args = JsonDefaults.ParseObject(parameters.GetValueOrDefault("args")) ?? [];
        var record = await commandService.InvokeServiceAsync(device, parameters["service"]!, args, $"Rule:{rule.Name}", 0, cancellationToken);

        return record.Status == ServiceStatusKinds.Failed ? RuleActionResult.Fail(record.OutputData ?? "failed") : RuleActionResult.Ok($"{device.Code} {record.Status}");
    }
}

/// <summary>写设备日志</summary>
public class DeviceLogAction : IRuleAction
{
    public String Name => "log";
    public String DisplayName => "Write device log";
    public String Description => "Append a message to the device history timeline and the platform log.";

    public IReadOnlyList<RuleActionParameter> Parameters { get; } =
    [
        new("message", "Message", "Supports templates", true, "{rule.name}: {device.name}"),
    ];

    public Task<RuleActionResult> ExecuteAsync(Rule rule, RuleContext context, IDictionary<String, String?> parameters, CancellationToken cancellationToken)
    {
        var message = parameters.GetValueOrDefault("message") ?? rule.Name;
        XTrace.WriteLine("[Rule:{0}] {1}", rule.Name, message);

        return Task.FromResult(RuleActionResult.Ok(message));
    }
}
