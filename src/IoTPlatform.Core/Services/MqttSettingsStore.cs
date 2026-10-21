using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife;

namespace IoTPlatform.Services;

/// <summary>把界面 MQTT 覆盖层持久化到 <c>{DataPath}/Config/mqtt-ui.json</c></summary>
public class MqttSettingsStore
{
    public const String FileName = "mqtt-ui.json";

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public String FilePath { get; }

    public MqttSettingsStore(String? directory = null)
    {
        var data = directory;
        if (String.IsNullOrWhiteSpace(data))
        {
            try { data = Setting.Current.DataPath; } catch { data = null; }
        }
        if (String.IsNullOrWhiteSpace(data)) data = Path.Combine(AppContext.BaseDirectory, "Data");

        var dir = Path.Combine(Path.GetFullPath(data), "Config");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, FileName);
    }

    public MqttUiSettings? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var json = File.ReadAllText(FilePath);
            if (String.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<MqttUiSettings>(json, Json);
        }
        catch (JsonException ex)
        {
            throw new PlatformException("MQTT UI settings file is invalid: {0}", ex.Message);
        }
    }

    public void Save(MqttUiSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, Json);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, FilePath, overwrite: true);
    }
}
