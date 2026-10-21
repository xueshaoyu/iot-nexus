using IoTPlatform.Data;
using Microsoft.Extensions.Options;
using NewLife;

namespace IoTPlatform.Services;

/// <summary>可选 MQTT 服务器（内置 + 外部配置）</summary>
public record MqttBrokerChoice(String Id, String Name, String Kind, String Server, String TopicRoot, Boolean IsDefault);

/// <summary>设备 → 产品 → 平台默认 的 MQTT 服务器解析，以及在用服务器集合</summary>
public class MqttBrokerCatalog(IOptions<IoTOptions> options)
{
    public MqttOptions Mqtt => options.Value.Mqtt;

    public String PlatformDefaultId => Mqtt.GetPlatformDefaultId();

    public Boolean IsEmbedded(String? id) => MqttBrokerIds.IsEmbedded(Canonicalize(id) ?? id);

    /// <summary>空/空白视为继承；内置规范为 embedded；外部按 Id 或名称归一到配置 Id</summary>
    public String? Canonicalize(String? id)
    {
        var n = MqttBrokerIds.Normalize(id);
        if (n == null) return null;
        if (MqttBrokerIds.IsEmbedded(n)) return MqttBrokerIds.Embedded;

        return Mqtt.FindBroker(n)?.Id ?? n;
    }

    public Boolean IsKnown(String? id)
    {
        var n = Canonicalize(id);
        if (n == null) return true;
        if (MqttBrokerIds.IsEmbedded(n)) return true;

        return Mqtt.FindBroker(n) != null;
    }

    public void EnsureKnown(String? id)
    {
        var n = MqttBrokerIds.Normalize(id);
        if (n == null) return;
        if (IsKnown(n)) return;

        throw new PlatformException("Unknown MQTT broker profile '{0}'", n);
    }

    /// <summary>解析设备实际使用的服务器：设备覆盖 → 产品默认 → 平台默认</summary>
    public String Resolve(Device device, Product? product = null)
    {
        product ??= device.Product;
        return Canonicalize(device.BrokerProfileId)
            ?? Canonicalize(product?.BrokerProfileId)
            ?? PlatformDefaultId;
    }

    public String Resolve(String? deviceBrokerId, String? productBrokerId) =>
        Canonicalize(deviceBrokerId) ?? Canonicalize(productBrokerId) ?? PlatformDefaultId;

    public String ResolveId(String? brokerProfileId) => Canonicalize(brokerProfileId) ?? PlatformDefaultId;

    public Boolean Same(String? a, String? b) =>
        String.Equals(ResolveId(a), ResolveId(b), StringComparison.OrdinalIgnoreCase);

    public String TopicRoot(String? brokerProfileId) => NormalizeRoot(Mqtt.TopicRootFor(ResolveId(brokerProfileId)));

    private static String NormalizeRoot(String? root)
    {
        root = root?.Trim().Trim('/');
        return String.IsNullOrEmpty(root) || root.Contains('+') || root.Contains('#') ? "iot" : root;
    }

    /// <summary>下拉选项：内置 + 已配置的外部服务器</summary>
    public IReadOnlyList<MqttBrokerChoice> Choices()
    {
        var mqtt = Mqtt;
        var def = PlatformDefaultId;
        var list = new List<MqttBrokerChoice>
        {
            new(MqttBrokerIds.Embedded, "Embedded (built-in)", MqttModes.Embedded, $"tcp://0.0.0.0:{mqtt.Port}", NormalizeRoot(mqtt.TopicRoot), Same(def, MqttBrokerIds.Embedded)),
        };

        foreach (var b in mqtt.GetBrokers())
        {
            if (b.Id.IsNullOrEmpty()) continue;
            list.Add(new(b.Id, b.Name.IsNullOrEmpty() ? b.Id : b.Name, MqttModes.External, b.Server, NormalizeRoot(Mqtt.TopicRootFor(b.Id)), Same(def, b.Id)));
        }

        return list;
    }

    public MqttBrokerChoice? FindChoice(String? id)
    {
        var n = ResolveId(id);
        return Choices().FirstOrDefault(e => e.Id.Equals(n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>已启用 MQTT 设备按解析后的服务器计数</summary>
    public Dictionary<String, Int32> CountEnabledDevices()
    {
        var counts = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in AssignedBrokerIds(enabledOnly: true, includeEmptyProducts: false))
            counts[id] = counts.GetValueOrDefault(id) + 1;

        return counts;
    }

    /// <summary>需要启动的服务器：模式默认 + 已启用 MQTT 产品/设备引用的配置</summary>
    public HashSet<String> RequiredIds()
    {
        var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        if (!Mqtt.Enable) return set;

        set.Add(PlatformDefaultId);

        foreach (var product in Product.FindAll().Where(e => e.Enable && e.Protocol == AccessProtocols.Mqtt))
        {
            var productDefault = Resolve(null, product.BrokerProfileId);
            set.Add(productDefault);
            foreach (var device in Device.FindAllByProductId(product.Id).Where(e => e.Enable))
                set.Add(Resolve(device, product));
        }

        foreach (var id in set.ToList())
        {
            if (MqttBrokerIds.IsEmbedded(id)) continue;
            var profile = Mqtt.FindBroker(id);
            if (profile == null || !profile.Enable) set.Remove(id);
        }

        return set;
    }

    private IEnumerable<String> AssignedBrokerIds(Boolean enabledOnly, Boolean includeEmptyProducts)
    {
        foreach (var product in Product.FindAll().Where(e => (!enabledOnly || e.Enable) && e.Protocol == AccessProtocols.Mqtt))
        {
            var devices = Device.FindAllByProductId(product.Id).Where(e => !enabledOnly || e.Enable).ToList();
            if (devices.Count == 0 && includeEmptyProducts)
            {
                yield return Resolve(null, product.BrokerProfileId);
                continue;
            }

            foreach (var device in devices)
                yield return Resolve(device, product);
        }
    }
}
