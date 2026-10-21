namespace IoTPlatform.Services;

/// <summary>界面保存的 MQTT 覆盖层。null 字段表示未改，沿用 appsettings / 默认值</summary>
public class MqttUiSettings
{
    public Boolean? Enable { get; set; }
    public String? Mode { get; set; }
    public String? TopicRoot { get; set; }
    public String? ActiveBroker { get; set; }
    public Int32? Port { get; set; }
    public String? PublicHost { get; set; }
    public String? AdminUser { get; set; }
    public String? AdminPassword { get; set; }
    public Boolean? AllowAnonymous { get; set; }
    public Int32? ManagementPort { get; set; }

    /// <summary>外部服务器完整列表。null 表示未在界面改过，沿用配置文件</summary>
    public List<ExternalMqttOptions>? Brokers { get; set; }
}

/// <summary>内置 Broker / 平台默认项的保存请求。密码空表示保持原值</summary>
public class MqttBuiltInInput
{
    public Boolean Enable { get; set; } = true;
    public String Mode { get; set; } = MqttModes.Embedded;
    public String TopicRoot { get; set; } = "iot";
    public String? ActiveBroker { get; set; }
    public Int32 Port { get; set; } = 1883;
    public String? PublicHost { get; set; }
    public String? AdminUser { get; set; }
    public String? AdminPassword { get; set; }
    public Boolean AllowAnonymous { get; set; }
    public Int32 ManagementPort { get; set; }
}

/// <summary>外部 MQTT 服务器保存请求。密码空表示保持原值</summary>
public class MqttBrokerInput
{
    public String? Id { get; set; }
    public String? Name { get; set; }
    public String? TopicRoot { get; set; }
    public String? Scheme { get; set; } = MqttBrokerEndpoint.Tcp;
    public String? Host { get; set; } = "127.0.0.1";
    public Int32 Port { get; set; } = 1883;
    public String? Path { get; set; }
    public String Server { get; set; } = "tcp://127.0.0.1:1883";
    public String ClientId { get; set; } = "newlife-iot-platform";
    public String? UserName { get; set; }
    public String? Password { get; set; }
    public Boolean UseTls { get; set; }
    public Int32 QoS { get; set; } = 1;
    public Int32 KeepAlive { get; set; } = 30;
    public Int32 ReconnectDelay { get; set; } = 5;
    public String? PublicServer { get; set; }
    public Boolean Enable { get; set; } = true;
}
