using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCADA.Models;
using SCADA.Services.Interfaces;

namespace SCADA.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly IModbusService _modbusService;

    [ObservableProperty] private ConnectionConfig _config;
    [ObservableProperty] private ConnectionState _state = ConnectionState.Disconnected;
    [ObservableProperty] private double _latencyMs = 0;
    [ObservableProperty] private long _packetsSent = 0;
    [ObservableProperty] private long _packetsReceived = 0;
    [ObservableProperty] private ushort _heartbeatCounter = 0;
    [ObservableProperty] private string _statusMessage = "Disconnected";
    [ObservableProperty] private string _statusColor = "#94A3B8"; // Slate 400
    [ObservableProperty] private bool _isHeartbeatActive = false;

    // Register Inspector test controls
    [ObservableProperty] private int _selectedRegisterAddress = 0;
    [ObservableProperty] private ushort _writeRegisterValue = 0;
    [ObservableProperty] private float _writeFloatValue = 0.0f;
    [ObservableProperty] private int _selectedCoilAddress = 0;
    [ObservableProperty] private bool _writeCoilValue = false;

    public ObservableCollection<ModbusRegisterItem> RegisterList { get; } = new();
    public ObservableCollection<LogMessage> LogList { get; } = new();

    public ConnectionViewModel(IModbusService modbusService, ConnectionConfig config)
    {
        _modbusService = modbusService;
        _config = config;

        InitializeRegisterList();

        _modbusService.StateChanged += OnConnectionStateChanged;
        _modbusService.LogReceived += OnLogReceived;
        _modbusService.RegistersUpdated += OnRegistersUpdated;
    }

    private void InitializeRegisterList()
    {
        RegisterList.Clear();
        RegisterList.Add(new ModbusRegisterItem { Address = 40001, Name = "Watchdog Ping", DataType = ModbusDataType.UInt16, AccessType = ModbusAccessType.ReadWrite, Unit = "count", Description = "Heartbeat ping/watchdog counter" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40002, Name = "Control Word", DataType = ModbusDataType.Bitfield16, AccessType = ModbusAccessType.ReadWrite, Unit = "hex", Description = "0x01:Enable, 0x02:Start, 0x04:E-Stop, 0x10:Reset" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40003, Name = "Status Word", DataType = ModbusDataType.Bitfield16, AccessType = ModbusAccessType.ReadOnly, Unit = "hex", Description = "0x01:Ready, 0x02:Moving, 0x04:InPos, 0x20:WatchdogOK" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40004, Name = "Target Q1", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "deg", Description = "Target Base Yaw Angle" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40006, Name = "Target Q2", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "deg", Description = "Target Shoulder Pitch Angle" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40008, Name = "Target Q3", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "deg", Description = "Target Elbow Pitch Angle" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40010, Name = "Target Q4", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "deg", Description = "Target Wrist Pitch Angle" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40012, Name = "Actual Q1", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "deg", Description = "Actual Base Yaw Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40014, Name = "Actual Q2", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "deg", Description = "Actual Shoulder Pitch Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40016, Name = "Actual Q3", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "deg", Description = "Actual Elbow Pitch Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40018, Name = "Actual Q4", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "deg", Description = "Actual Wrist Pitch Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40020, Name = "Target X", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "mm", Description = "Target Cartesian X Position" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40022, Name = "Target Y", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "mm", Description = "Target Cartesian Y Position" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40024, Name = "Target Z", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "mm", Description = "Target Cartesian Z Position" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40026, Name = "Target Pitch", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadWrite, Unit = "deg", Description = "Target Wrist Pitch Angle" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40028, Name = "Actual X", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "mm", Description = "Actual Cartesian X Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40030, Name = "Actual Y", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "mm", Description = "Actual Cartesian Y Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40032, Name = "Actual Z", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "mm", Description = "Actual Cartesian Z Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40034, Name = "Actual Pitch", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "deg", Description = "Actual Cartesian Pitch Feedback" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40036, Name = "Torque Tau1", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "N*m", Description = "Joint 1 dynamic torque" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40038, Name = "Torque Tau2", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "N*m", Description = "Joint 2 dynamic torque" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40040, Name = "Torque Tau3", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "N*m", Description = "Joint 3 dynamic torque" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40042, Name = "Torque Tau4", DataType = ModbusDataType.Float32, AccessType = ModbusAccessType.ReadOnly, Unit = "N*m", Description = "Joint 4 dynamic torque" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40044, Name = "Gripper Output", DataType = ModbusDataType.UInt16, AccessType = ModbusAccessType.ReadWrite, Unit = "", Description = "0: Open, 1: Closed" });
        RegisterList.Add(new ModbusRegisterItem { Address = 40045, Name = "Error Code", DataType = ModbusDataType.UInt16, AccessType = ModbusAccessType.ReadOnly, Unit = "", Description = "0: OK, 1: Limit, 2: E-Stop, 3: Watchdog Trip" });
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        await _modbusService.ConnectAsync(Config);
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        await _modbusService.DisconnectAsync();
    }

    [RelayCommand]
    public async Task WriteRegisterAsync()
    {
        if (SelectedRegisterAddress < 0) return;
        await _modbusService.WriteSingleRegisterAsync(SelectedRegisterAddress, WriteRegisterValue);
    }

    [RelayCommand]
    public async Task WriteFloatRegisterAsync()
    {
        if (SelectedRegisterAddress < 0) return;
        await _modbusService.WriteFloatRegistersAsync(SelectedRegisterAddress, WriteFloatValue, Config.FloatEndianness);
    }

    [RelayCommand]
    public async Task WriteCoilAsync()
    {
        if (SelectedCoilAddress < 0) return;
        await _modbusService.WriteSingleCoilAsync(SelectedCoilAddress, WriteCoilValue);
    }

    [RelayCommand]
    public void ClearLogs()
    {
        LogList.Clear();
    }

    private void OnConnectionStateChanged(ConnectionState newState)
    {
        Dispatcher.UIThread.Post(() =>
        {
            State = newState;
            StatusMessage = newState switch
            {
                ConnectionState.Connected => Config.IsSimulationMode ? "Connected (Virtual OPTA)" : "Connected (Hardware)",
                ConnectionState.Connecting => "Connecting...",
                ConnectionState.WatchdogTimeout => "WATCHDOG TIMEOUT",
                ConnectionState.Error => "Connection Error",
                _ => "Disconnected"
            };

            StatusColor = newState switch
            {
                ConnectionState.Connected => "#10B981", // Green
                ConnectionState.Connecting => "#F59E0B", // Amber
                ConnectionState.WatchdogTimeout => "#EF4444", // Red
                ConnectionState.Error => "#EF4444", // Red
                _ => "#94A3B8" // Slate
            };
        });
    }

    private void OnLogReceived(LogMessage log)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogList.Insert(0, log);
            if (LogList.Count > 200)
            {
                LogList.RemoveAt(LogList.Count - 1);
            }
        });
    }

    private void OnRegistersUpdated(ushort[] regs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LatencyMs = _modbusService.LatencyMs;
            PacketsSent = _modbusService.PacketsSent;
            PacketsReceived = _modbusService.PacketsReceived;
            HeartbeatCounter = _modbusService.HeartbeatCounter;
            IsHeartbeatActive = !IsHeartbeatActive; // toggle pulse for heartbeat LED

            if (regs.Length >= 46)
            {
                UpdateRegisterItem(0, regs[0]); // Watchdog
                UpdateRegisterItem(1, $"0x{regs[1]:X4}"); // Control Word
                UpdateRegisterItem(2, $"0x{regs[2]:X4}"); // Status Word

                UpdateFloatItem(3, regs[3], regs[4]);   // Target Q1
                UpdateFloatItem(4, regs[5], regs[6]);   // Target Q2
                UpdateFloatItem(5, regs[7], regs[8]);   // Target Q3
                UpdateFloatItem(6, regs[9], regs[10]);  // Target Q4

                UpdateFloatItem(7, regs[11], regs[12]); // Actual Q1
                UpdateFloatItem(8, regs[13], regs[14]); // Actual Q2
                UpdateFloatItem(9, regs[15], regs[16]); // Actual Q3
                UpdateFloatItem(10, regs[17], regs[18]);// Actual Q4

                UpdateFloatItem(11, regs[19], regs[20]); // Target X
                UpdateFloatItem(12, regs[21], regs[22]); // Target Y
                UpdateFloatItem(13, regs[23], regs[24]); // Target Z
                UpdateFloatItem(14, regs[25], regs[26]); // Target Pitch

                UpdateFloatItem(15, regs[27], regs[28]); // Actual X
                UpdateFloatItem(16, regs[29], regs[30]); // Actual Y
                UpdateFloatItem(17, regs[31], regs[32]); // Actual Z
                UpdateFloatItem(18, regs[33], regs[34]); // Actual Pitch

                UpdateFloatItem(19, regs[35], regs[36]); // Tau1
                UpdateFloatItem(20, regs[37], regs[38]); // Tau2
                UpdateFloatItem(21, regs[39], regs[40]); // Tau3
                UpdateFloatItem(22, regs[41], regs[42]); // Tau4

                UpdateRegisterItem(23, regs[43] == 1 ? "Closed" : "Open"); // Gripper
                UpdateRegisterItem(24, regs[44]); // Error Code
            }
        });
    }

    private void UpdateRegisterItem(int index, object val)
    {
        if (index >= 0 && index < RegisterList.Count)
        {
            RegisterList[index].Value = val;
            RegisterList[index].FormattedValue = val.ToString() ?? "";
        }
    }

    private void UpdateFloatItem(int index, ushort high, ushort low)
    {
        if (index >= 0 && index < RegisterList.Count)
        {
            ushort hw = Config.FloatEndianness == Endianness.LittleEndian_CDAB ? low : high;
            ushort lw = Config.FloatEndianness == Endianness.LittleEndian_CDAB ? high : low;

            byte[] bytes = new byte[4];
            bytes[3] = (byte)(hw >> 8);
            bytes[2] = (byte)(hw & 0xFF);
            bytes[1] = (byte)(lw >> 8);
            bytes[0] = (byte)(lw & 0xFF);

            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            float f = BitConverter.ToSingle(bytes, 0);

            RegisterList[index].Value = f;
            RegisterList[index].FormattedValue = $"{f:F2}";
        }
    }
}
