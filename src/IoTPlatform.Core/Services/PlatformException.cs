using System.Globalization;

namespace IoTPlatform.Services;

/// <summary>业务异常。表示请求本身不合法（不存在、冲突、参数错误），接口层映射为 4xx</summary>
/// <remarks>消息由模板和参数组成，界面层可按当前语言翻译 <see cref="Template"/> 后再格式化</remarks>
public class PlatformException : Exception
{
    public Int32 StatusCode { get; }

    /// <summary>消息模板（英文，作为本地化键），参数占位符为 {0} {1}</summary>
    public String Template { get; }

    public Object?[] Arguments { get; }

    public PlatformException(String template, params Object?[] args) : this(400, template, args) { }

    public PlatformException(Int32 statusCode, String template, params Object?[] args)
        : base(args.Length == 0 ? template : String.Format(CultureInfo.InvariantCulture, template, args))
    {
        StatusCode = statusCode;
        Template = template;
        Arguments = args;
    }

    public static PlatformException NotFound(String what) => new(404, $"{what} not found");
}
