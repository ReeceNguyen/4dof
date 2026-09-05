using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
namespace OptaLedController.Services;

public class MqttServices
{
    // public event Action<bool>? OnLedStatusReceived;
    protected readonly IMqttClient _mqttClient;
    protected readonly MqttConfig _config;

    public MqttServices() :this(new MqttConfig()){}
    public MqttServices(IMqttClient client, MqttConfig config)
    {
        _mqttClient = client;
        _config = config;
    }

    public MqttServices(MqttConfig config)
    {
        _config = config;
        _mqttClient = new MqttClientFactory().CreateMqttClient();
    }

    public async Task ConnectAsync()
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_config.Uri, _config.Port)
            .WithCredentials(_config.Username, _config.Password)
            .WithClientId(_config.ClientId)
            .Build();
        await _mqttClient.ConnectAsync(options);
    }
}