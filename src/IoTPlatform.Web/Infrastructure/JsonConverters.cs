using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IoTPlatform.Web.Infrastructure;

/// <summary>超出 JavaScript 安全整数范围的 Int64（雪花Id）输出为字符串，读取时兼容数字与字符串</summary>
public class SafeInt64Converter : JsonConverter<Int64>
{
    private const Int64 MaxSafe = 9_007_199_254_740_991;

    public override Int64 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? Int64.Parse(reader.GetString()!, CultureInfo.InvariantCulture) : reader.GetInt64();

    public override void Write(Utf8JsonWriter writer, Int64 value, JsonSerializerOptions options)
    {
        if (value > MaxSafe || value < -MaxSafe)
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
        else
            writer.WriteNumberValue(value);
    }
}

/// <summary>统一输出本地时间，不带时区偏移；最小值输出 null</summary>
public class LocalDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? DateTime.MinValue : reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        if (value.Year <= 1)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
    }
}
