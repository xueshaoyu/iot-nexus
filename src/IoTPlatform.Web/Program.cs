using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using IoTPlatform;
using IoTPlatform.Mqtt;
using IoTPlatform.Protocols;
using IoTPlatform.Services;
using IoTPlatform.Web;
using IoTPlatform.Web.Components;
using IoTPlatform.Web.Infrastructure;
using IoTPlatform.Web.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using NewLife.Log;
using IoTPlatform.Data;

// Must run before XTrace / XCodeSetting: IoTPlatform.Web matches NewLife's *Web host rule
// and would otherwise resolve DataPath to ../Data → /Data when cwd is /app.
NewLifeHostPaths.Apply();
XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// 数据库：XCode 根据实体自动建表/加字段（反向工程），默认 SQLite
XCode.XCodeSetting.Current.ShowSQL = builder.Configuration.GetValue("XCode:ShowSQL", false);
IoTDatabase.Init(
    builder.Configuration.GetConnectionString("IoT") ?? "Data Source=Data/iot.db",
    builder.Configuration["ConnectionStrings:IoTProvider"] ?? "SQLite");

services.Configure<IoTOptions>(builder.Configuration.GetSection(IoTOptions.SectionName));

// 平台分层：核心服务 → 协议/驱动 → MQTT 接入
services.AddIoTCore();
services.AddIoTProtocols();
services.AddIoTMqtt();

// 后台任务
services.AddHostedService<OnlineMonitorWorker>();
services.AddHostedService<StatWorker>();
services.AddHostedService<DemoSimulatorWorker>();

services.AddSingleton<DataSeeder>();
services.AddSingleton<ConnectionInfoService>();
services.AddScoped<ToastService>();

// 管理接口
services.AddControllers(o => o.Filters.Add<PlatformExceptionFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new SafeInt64Converter());
        o.JsonSerializerOptions.Converters.Add(new LocalDateTimeConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        o.JsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { EntityJsonContract.Modify } };
    });
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(SwaggerSetup.Configure);

// 管理界面与多语言：键为英文原文，翻译位于 Resources/SharedResource.{culture}.resx
services.AddLocalization(o => o.ResourcesPath = "Resources");
services.Configure<RequestLocalizationOptions>(o =>
{
    o.SetDefaultCulture(UiCultures.Default);
    o.SupportedCultures = UiCultures.Cultures;
    o.SupportedUICultures = UiCultures.Cultures;
    // 仅认可显式选择（?culture= 或 Cookie），未选择时默认中文
    o.RequestCultureProviders = [new QueryStringRequestCultureProvider(), new CookieRequestCultureProvider()];
});
services.AddSingleton(System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));
services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
Fmt.Localizer = app.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

// 初始化：清理上次运行残留的在线状态，写入演示数据
using (var scope = app.Services.CreateScope())
{
    var options = app.Configuration.GetSection(IoTOptions.SectionName).Get<IoTOptions>() ?? new IoTOptions();
    scope.ServiceProvider.GetRequiredService<DeviceOnlineService>().Reset();
    if (options.Demo.SeedData) scope.ServiceProvider.GetRequiredService<DataSeeder>().Seed(options);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NewLife IoT Platform API v1");
    c.DocumentTitle = "NewLife IoT Platform API";
});

app.UseMiddleware<AccessLogMiddleware>();
app.UseRequestLocalization();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapGet("/culture/set", (HttpContext ctx, String? culture, String? redirectUri) =>
{
    UiCultures.Apply(ctx, culture);
    return Results.LocalRedirect(String.IsNullOrEmpty(redirectUri) || !redirectUri.StartsWith('/') ? "/" : redirectUri);
}).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.Now })).ExcludeFromDescription();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

/// <summary>集成测试入口</summary>
public partial class Program;
