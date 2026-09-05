using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using OptaLedController.Services;
namespace OptaLedController.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly MqttSubscriber _mqttClient;
    private readonly JsonDataLogger _dataLogger;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]


    // Thuộc tính phụ thuộc vào IsLedOn để đổi màu
    private bool _isLedOn;
    public string LedColor => IsLedOn ? "Green" : "Red";
    public string StatusText => IsLedOn ? "Trạng thái: Đang BẬT (1)" : "Trạng thái: Đang TẮT (0)";
    // Gọi thông báo cập nhật LedColor khi IsLedOn thay đổi
    partial void OnIsLedOnChanged(bool value)
    {
        OnPropertyChanged(nameof(LedColor));
        _ = _dataLogger.SaveStateAsync(value);
    }

    [ObservableProperty]
    private string _connectionStatus = "Đang kết nối...";

    public MainViewModel()
    {
        _dataLogger = new JsonDataLogger();
        _mqttClient = new MqttSubscriber();
        _mqttClient.OnLedStatusReceived += status =>
        {
            Dispatcher.UIThread.Post(() => IsLedOn = status);
        };
        _ = InitMqttAsync();
    }

    private async Task InitMqttAsync()
    {
// 1. ĐỌC DỮ LIỆU JSON ĐỂ KHÔI PHỤC TRẠNG THÁI GẦN NHẤT KHI VÀO LẠI APP
        var savedState = await _dataLogger.LoadStateAsync();
        if (savedState != null)
        {
            Dispatcher.UIThread.Post(() => IsLedOn = savedState.IsLedOn);
        }

//2. KẾT NỐI MQTT BROKER
        try
        {
            await _mqttClient.ConnectAsync();
            ConnectionStatus = "Đã kết nối MQTT Broker";
// 3. ĐỒNG BỘ TRẠNG THÁI KHÔI PHỤC TỪ JSON LÊN BROKER/OPTA (NẾU CÓ DỮ LIỆU LƯU CŨ)
            if (savedState != null)
            {
                await _mqttClient.SendLedCommandAsync(savedState.IsLedOn);
            }
        }
        catch
        {
            ConnectionStatus = "Kết nối Broker thất bại!";
        }
        await _mqttClient.SubscribeLedStatusAsync("opta/led/status");
    }

    [RelayCommand]
    private async Task ToggleLedAsync()
    {
        await _mqttClient.SendLedCommandAsync(!IsLedOn);
    }
    
}