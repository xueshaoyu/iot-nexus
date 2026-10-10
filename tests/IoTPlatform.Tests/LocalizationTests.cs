using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IoTPlatform.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Tests;

/// <summary>保证管理界面的中文翻译完整、资源装配正确</summary>
public class LocalizationTests
{
    private static readonly String Root = FindRoot();
    private static readonly String WebDir = Path.Combine(Root, "src", "IoTPlatform.Web");

    private static String FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IoTPlatform.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("IoTPlatform.sln not found");
    }

    private static Dictionary<String, String> LoadResx(String culture) =>
        XDocument.Load(Path.Combine(WebDir, "Resources", $"SharedResource.{culture}.resx")).Root!
            .Elements("data").ToDictionary(e => (String)e.Attribute("name")!, e => (String)e.Element("value")!);

    /// <summary>界面中所有字面量本地化键：L["..."] / Loc["..."] / Fmt.Tr("...") / Fmt.Text("...") / Fmt 内部 T("...")</summary>
    private static IEnumerable<String> UiKeys()
    {
        var files = Directory.GetFiles(Path.Combine(WebDir, "Components"), "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(WebDir, "Components"), "*.cs", SearchOption.AllDirectories))
            .Append(Path.Combine(WebDir, "Services", "Fmt.cs"));
        var patterns = new[] { new Regex(@"\b(?:L|Loc)\[""((?:[^""\\]|\\.)*)"""), new Regex(@"\b(?:Fmt\.Tr|Fmt\.Text|T)\(""((?:[^""\\]|\\.)*)""") };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var p in patterns)
                foreach (Match m in p.Matches(text)) yield return Regex.Unescape(m.Groups[1].Value);
        }
    }

    [Fact]
    public void Chinese_CoversEveryUiString()
    {
        var zh = LoadResx("zh-CN");
        var missing = UiKeys().Distinct().Where(k => !zh.ContainsKey(k)).OrderBy(e => e).ToList();

        Assert.True(missing.Count == 0, "Missing zh-CN translations:\n" + String.Join("\n", missing));
    }

    [Fact]
    public void Chinese_CoversServiceErrorTemplates()
    {
        var zh = LoadResx("zh-CN");
        var templates = Directory.GetFiles(Path.Combine(Root, "src", "IoTPlatform.Core"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"new PlatformException\((?:\d{3}, )?""((?:[^""\\]|\\.)*)""").Select(m => m.Groups[1].Value)
                .Concat(Regex.Matches(File.ReadAllText(f), @"NotFound\(""([^""]+)""\)").Select(m => m.Groups[1].Value + " not found")))
            .Distinct().ToList();

        Assert.NotEmpty(templates);
        var missing = templates.Where(t => !zh.ContainsKey(t)).ToList();
        Assert.True(missing.Count == 0, "Missing zh-CN translations for service errors:\n" + String.Join("\n", missing));
    }

    [Fact]
    public void Resources_HaveNoCaseInsensitiveDuplicates()
    {
        // .resources 生成时按不区分大小写去重，重复键会被静默丢弃
        foreach (var culture in new[] { "zh-CN", "en" })
        {
            var dupes = LoadResx(culture).Keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(dupes.Count == 0, $"{culture} duplicate keys: {String.Join(", ", dupes)}");
        }
    }

    [Fact]
    public void Localizer_ReturnsChineseForZhCn_AndKeyForEnglish()
    {
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        var L = factory.Create(typeof(SharedResource));
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("zh-CN");
            Assert.Equal("概览", L["Dashboard"].Value);
            Assert.Equal("在线率 91.7%", L["{0}% online rate", "91.7"].Value);

            CultureInfo.CurrentUICulture = new CultureInfo("en");
            Assert.Equal("Dashboard", L["Dashboard"].Value);
            Assert.Equal("Modbus TCP", L["TCP网络版Modbus"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void TimeTexts_DoNotWrap()
    {
        // 时间文本使用不换行空格，表格窄列中不会被拆成两行
        var t = DateTime.Now.AddMinutes(-3).AddDays(-2);
        foreach (var text in new[] { IoTPlatform.Web.Services.Fmt.Full(t), IoTPlatform.Web.Services.Fmt.Time(t), IoTPlatform.Web.Services.Fmt.Ago(DateTime.Now.AddMinutes(-3)), IoTPlatform.Web.Services.Fmt.Duration(3723) })
            Assert.DoesNotContain(' ', text);
    }

    [Fact]
    public void DefaultCulture_IsChinese() => Assert.Equal("zh-CN", UiCultures.Default);
}
