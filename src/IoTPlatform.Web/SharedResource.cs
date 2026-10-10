using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace IoTPlatform.Web;

/// <summary>界面共享资源标记类。键为英文原文，翻译位于 Resources/SharedResource.{culture}.resx</summary>
public class SharedResource
{
}

/// <summary>界面语言配置</summary>
public static class UiCultures
{
    public const String Default = "zh-CN";

    /// <summary>支持的语言。新增语言：在此追加并添加对应的 SharedResource.{culture}.resx</summary>
    public static readonly (String Name, String Label)[] Supported =
    [
        ("zh-CN", "简体中文"),
        ("en", "English"),
    ];

    public static CultureInfo[] Cultures => Supported.Select(e => new CultureInfo(e.Name)).ToArray();

    /// <summary>写入语言 Cookie，默认中文</summary>
    public static void Apply(HttpContext context, String? culture)
    {
        var name = Supported.Any(e => e.Name.Equals(culture, StringComparison.OrdinalIgnoreCase)) ? culture! : Default;
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(name)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
    }
}
