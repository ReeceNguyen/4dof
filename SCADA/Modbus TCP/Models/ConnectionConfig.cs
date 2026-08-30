using CommunityToolkit.Mvvm.ComponentModel;

namespace MobusTCP.Models;

public enum Endianness
{
    BigEndian_ABCD,
    LittleEndian_CDAB,
    ByteSwap_BADC,
    WordByteSwap_DCBA
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    WatchdogTimeout,
    Error
}

public partial class ConnectionConfig : ObservableObject
{
    [ObservableProperty] private string _ipAddress = "127.0.0.1";
    [ObservableProperty] private int _port = 502;
    [ObservableProperty] private byte _unitId = 1;
    [ObservableProperty] private int _watchdogTimeoutMs = 2000;
    [ObservableProperty] private int _pollingIntervalMs = 50;
    [ObservableProperty] private bool _isSimulationMode = true;
    [ObservableProperty] private Endianness _floatEndianness = Endianness.BigEndian_ABCD;
    [ObservableProperty] private bool _autoReconnect = true;
    [ObservableProperty] private int _reconnectIntervalMs = 3000;
}
