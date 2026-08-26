using System;
using System.Threading.Tasks;
using SCADA.Models;

namespace SCADA.Services.Interfaces;

public interface IModbusService
{
    ConnectionState State { get; }
    bool IsConnected { get; }
    double LatencyMs { get; }
    long PacketsSent { get; }
    long PacketsReceived { get; }
    ushort HeartbeatCounter { get; }

    event Action<ConnectionState>? StateChanged;
    event Action<LogMessage>? LogReceived;
    event Action<ushort[]>? RegistersUpdated;

    Task<bool> ConnectAsync(ConnectionConfig config);
    Task DisconnectAsync();

    Task<ushort[]?> ReadHoldingRegistersAsync(int startAddress, int count);
    Task<bool> WriteSingleRegisterAsync(int address, ushort value);
    Task<bool> WriteMultipleRegistersAsync(int startAddress, ushort[] values);
    Task<bool> WriteFloatRegistersAsync(int startAddress, float value, Endianness endianness);
    Task<float?> ReadFloatRegistersAsync(int startAddress, Endianness endianness);

    Task<bool[]?> ReadCoilsAsync(int startAddress, int count);
    Task<bool> WriteSingleCoilAsync(int address, bool value);

    Task<bool> SendJointTargetsAsync(double q1, double q2, double q3, double q4);
    Task<bool> SendCartesianTargetsAsync(double x, double y, double z, double pitch);
    Task<bool> SendEmergencyStopAsync();
    Task<bool> SendResetErrorAsync();
    Task<bool> SendGripperCommandAsync(bool close);
}
