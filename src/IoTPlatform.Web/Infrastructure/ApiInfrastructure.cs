using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using XCode;
using XCode.Configuration;

namespace IoTPlatform.Web.Infrastructure;

/// <summary>XCode 实体的 Json 契约：只输出数据字段与 [Map] 关联显示字段（如 ProductName），忽略导航属性与内部状态</summary>
public static class EntityJsonContract
{
    public static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !typeof(IEntity).IsAssignableFrom(typeInfo.Type)) return;

        var allowed = GetAllowed(typeInfo.Type);
        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var name = (typeInfo.Properties[i].AttributeProvider as MemberInfo)?.Name;
            if (name == null || !allowed.Contains(name)) typeInfo.Properties.RemoveAt(i);
        }
    }

    public static HashSet<String> GetAllowed(Type type)
    {
        var set = new HashSet<String>(StringComparer.Ordinal);
        var factory = EntityFactory.CreateFactory(type);
        foreach (var f in factory.Fields) set.Add(f.Name);
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetCustomAttribute<MapAttribute>() != null) set.Add(p.Name);
        }

        return set;
    }
}

/// <summary>Swagger 中实体模型与 Json 契约保持一致</summary>
public class EntitySchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!typeof(IEntity).IsAssignableFrom(context.Type) || schema.Properties == null) return;

        var allowed = EntityJsonContract.GetAllowed(context.Type).Select(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName).ToHashSet();
        foreach (var key in schema.Properties.Keys.Where(k => !allowed.Contains(k)).ToList()) schema.Properties.Remove(key);
    }
}

/// <summary>把业务异常转为 ProblemDetails</summary>
public class PlatformExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, message) = context.Exception switch
        {
            PlatformException pe => (pe.StatusCode, pe.Message),
            XCodeException xe when xe.Message.Contains("已存在") => (409, xe.Message),
            ArgumentException ae => (400, ae.Message),
            _ => (0, null),
        };
        if (status == 0) return;

        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = message, Detail = message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

/// <summary>管理接口密钥校验。配置 IoT:ApiKey 后生效，设备接入接口使用设备密钥单独认证</summary>
public class ApiKeyMiddleware(RequestDelegate next, IOptions<IoTOptions> options)
{
    public const String HeaderName = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var key = options.Value.ApiKey;
        var path = context.Request.Path;
        if (!String.IsNullOrEmpty(key) && path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/device-access"))
        {
            var provided = context.Request.Headers[HeaderName].FirstOrDefault() ?? context.Request.Query["api_key"].FirstOrDefault();
            if (provided != key)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 401, Title = $"Missing or invalid {HeaderName}" });
                return;
            }
        }

        await next(context);
    }
}

public static class SwaggerSetup
{
    public static void Configure(SwaggerGenOptions c)
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "NewLife IoT Platform API",
            Version = "v1",
            Description = "Management API (products, thing models, devices, groups, rules, alarms, statistics, protocols) and device HTTP access API.",
        });
        c.SchemaFilter<EntitySchemaFilter>();
        c.CustomSchemaIds(t => t.FullName?.Replace("+", ".").Replace("`1", "Of"));
        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme { Name = ApiKeyMiddleware.HeaderName, In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }] = [],
        });
    }
}
