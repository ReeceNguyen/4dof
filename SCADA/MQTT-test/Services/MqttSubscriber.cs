using System.Threading.Tasks;
using System.Text;
using System;
using MQTTnet;

namespace OptaLedController.Services;

public class MqttSubscriber : MqttPublisher
{
    public event Action<bool>? OnLedStatusReceived;

    public MqttSubscriber() : this(new MqttConfig())
    {
    }

    public MqttSubscriber(IMqttClient client, MqttConfig config) : base(client, config)
    {
        SetupHandler();
    }

    public MqttSubscriber(MqttConfig config) : base(config)
    {
        SetupHandler();
    }

    private void SetupHandler()
    {
        // GIỮ NGUYÊN ĐOẠN CODE ĐỌC PAYLOAD CỦA BẠN TẠI ĐÂY
        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            if (topic == "opta/led/status")
            {
                var isLedOn = payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase);
                OnLedStatusReceived?.Invoke(isLedOn);
            }

            return Task.CompletedTask;
        };
    }

    public async Task SubscribeLedStatusAsync(string topic)
    {
        // Kiểm tra kết nối an toàn trước khi Subscribe
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            await _mqttClient.SubscribeAsync(topic);
        }
    }
}