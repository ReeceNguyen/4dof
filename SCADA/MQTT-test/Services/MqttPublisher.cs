using MQTTnet;
using System.Threading.Tasks;
namespace OptaLedController.Services;

public class MqttPublisher : MqttServices
{
    public MqttPublisher():this(new MqttConfig()){}
    public MqttPublisher(IMqttClient client, MqttConfig config ) : base(client, config){}
    public MqttPublisher(MqttConfig config) : base(config){}
    public async Task SendLedCommandAsync(bool turnOn)
    {
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("opta/led/status")
                .WithPayload(turnOn ? "1" : "0")
                .WithRetainFlag(true)
                .Build();
    
            await _mqttClient.PublishAsync(message);
        }
    }
}