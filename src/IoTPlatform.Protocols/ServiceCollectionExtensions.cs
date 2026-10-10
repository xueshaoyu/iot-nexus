using IoTPlatform.Protocols.Codecs;
using IoTPlatform.Protocols.Drivers;
using IoTPlatform.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IoTPlatform.Protocols;

public static class ProtocolServiceCollectionExtensions
{
    /// <summary>注册协议层：载荷编解码器、NewLife.IoT 驱动主机</summary>
    public static IServiceCollection AddIoTProtocols(this IServiceCollection services)
    {
        services.AddSingleton<IPayloadCodec, JsonPayloadCodec>();
        services.AddSingleton<IPayloadCodec, AlinkPayloadCodec>();
        services.AddSingleton<PayloadCodecFactory>();

        services.AddSingleton<DriverHostService>();
        services.AddSingleton<IDeviceCommandChannel>(sp => sp.GetRequiredService<DriverHostService>());
        services.AddHostedService(sp => sp.GetRequiredService<DriverHostService>());

        return services;
    }
}
