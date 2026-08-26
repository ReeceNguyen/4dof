using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SCADA.Models;
using SCADA.Services.Interfaces;

namespace SCADA.Services;

public class ModbusTcpService : IModbusService, IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private readonly SemaphoreSlim _socketLock = new(1, 1);

    private ConnectionConfig _config = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private ushort _transactionId = 0;
    private ushort _heartbeatCounter = 0;
    private double _latencyMs = 0;
    private long _packetsSent = 0;
    private long _packetsReceived = 0;

    private readonly VirtualOptaSimulator _simulator = new();
    private CancellationTokenSource? _pollCts;
    private Task? _pollLoopTask;
    private DateTime _lastSuccessfulResponseTime = DateTime.MinValue;

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(_state);
            }
        }
    }

    public bool IsConnected => State == ConnectionState.Connected;
    public double LatencyMs => _latencyMs;
    public long PacketsSent => _packetsSent;
    public long PacketsReceived => _packetsReceived;
    public ushort HeartbeatCounter => _heartbeatCounter;

    public event Action<ConnectionState>? StateChanged;
    public event Action<LogMessage>? LogReceived;
    public event Action<ushort[]>? RegistersUpdated;

    public async Task<bool> ConnectAsync(ConnectionConfig config)
    {
        _config = config;
        State = ConnectionState.Connecting;
        Log(LogLevel.Info, $"Connecting to OPTA Codesys at {config.IpAddress}:{config.Port} (Mode: {(config.IsSimulationMode ? "Virtual Simulator" : "TCP Hardware")})...");

        try
        {
            if (config.IsSimulationMode)
            {
                _simulator.Start();
                _lastSuccessfulResponseTime = DateTime.UtcNow;
                State = ConnectionState.Connected;
                Log(LogLevel.Success, "Connected to Virtual OPTA Codesys Simulator successfully.");
            }
            else
            {
                await _socketLock.WaitAsync();
                try
                {
                    _tcpClient?.Dispose();
                    _tcpClient = new TcpClient();
                    _tcpClient.SendTimeout = config.WatchdogTimeoutMs;
                    _tcpClient.ReceiveTimeout = config.WatchdogTimeoutMs;

                    var connectTask = _tcpClient.ConnectAsync(config.IpAddress, config.Port);
                    var timeoutTask = Task.Delay(config.WatchdogTimeoutMs);

                    if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                    {
                        throw new TimeoutException($"TCP Connection timed out after {config.WatchdogTimeoutMs} ms.");
                    }

                    await connectTask; // propagate any exception
                    _networkStream = _tcpClient.GetStream();
                }
                finally
                {
                    _socketLock.Release();
                }

                _lastSuccessfulResponseTime = DateTime.UtcNow;
                State = ConnectionState.Connected;
                Log(LogLevel.Success, $"Connected to OPTA Codesys PLC at {config.IpAddress}:{config.Port}");
            }

            StartPollingLoop();
            return true;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            Log(LogLevel.Error, $"Connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        Log(LogLevel.Info, "Disconnecting from OPTA Codesys...");
        StopPollingLoop();

        if (_config.IsSimulationMode)
        {
            _simulator.Stop();
        }
        else
        {
            await _socketLock.WaitAsync();
            try
            {
                _networkStream?.Dispose();
                _networkStream = null;
                _tcpClient?.Dispose();
                _tcpClient = null;
            }
            finally
            {
                _socketLock.Release();
            }
        }

        State = ConnectionState.Disconnected;
        Log(LogLevel.Info, "Disconnected.");
    }

    private void StartPollingLoop()
    {
        StopPollingLoop();
        _pollCts = new CancellationTokenSource();
        _pollLoopTask = Task.Run(() => PollingLoop(_pollCts.Token));
    }

    private void StopPollingLoop()
    {
        _pollCts?.Cancel();
        _pollLoopTask = null;
    }

    private async Task PollingLoop(CancellationToken token)
    {
        var stopwatch = new Stopwatch();

        while (!token.IsCancellationRequested)
        {
            try
            {
                stopwatch.Restart();

                // 1. Send Heartbeat increment
                _heartbeatCounter++;
                await WriteSingleRegisterAsync(0, _heartbeatCounter);

                // 2. Read Robot Holding Registers (0 to 45)
                var regs = await ReadHoldingRegistersAsync(0, 46);
                stopwatch.Stop();
                _latencyMs = stopwatch.Elapsed.TotalMilliseconds;

                if (regs != null && regs.Length > 0)
                {
                    _lastSuccessfulResponseTime = DateTime.UtcNow;
                    RegistersUpdated?.Invoke(regs);

                    if (State != ConnectionState.Connected)
                    {
                        State = ConnectionState.Connected;
                    }
                }

                // 3. Check Watchdog Timeout
                var elapsedSinceLastResponse = (DateTime.UtcNow - _lastSuccessfulResponseTime).TotalMilliseconds;
                if (elapsedSinceLastResponse > _config.WatchdogTimeoutMs)
                {
                    if (State != ConnectionState.WatchdogTimeout)
                    {
                        State = ConnectionState.WatchdogTimeout;
                        Log(LogLevel.Error, $"WATCHDOG TIMEOUT: No response from OPTA PLC for {elapsedSinceLastResponse:F0} ms (Threshold: {_config.WatchdogTimeoutMs} ms)");
                    }
                }

                await Task.Delay(Math.Max(10, _config.PollingIntervalMs), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"Polling cycle error: {ex.Message}");
                await Task.Delay(500, token);
            }
        }
    }

    public async Task<ushort[]?> ReadHoldingRegistersAsync(int startAddress, int count)
    {
        if (_config.IsSimulationMode)
        {
            _packetsSent++;
            _packetsReceived++;
            return _simulator.ReadHoldingRegisters(startAddress, count);
        }

        if (_networkStream == null || !_tcpClient!.Connected) return null;

        await _socketLock.WaitAsync();
        try
        {
            ushort transId = unchecked(++_transactionId);
            byte[] request = new byte[12];
            request[0] = (byte)(transId >> 8);
            request[1] = (byte)(transId & 0xFF);
            request[2] = 0x00; // Protocol ID (Modbus = 0)
            request[3] = 0x00;
            request[4] = 0x00; // Length = 6
            request[5] = 0x06;
            request[6] = _config.UnitId;
            request[7] = 0x03; // FC03 Read Holding Registers
            request[8] = (byte)(startAddress >> 8);
            request[9] = (byte)(startAddress & 0xFF);
            request[10] = (byte)(count >> 8);
            request[11] = (byte)(count & 0xFF);

            await _networkStream.WriteAsync(request, 0, request.Length);
            _packetsSent++;

            // Read MBAP Header (7 bytes)
            byte[] header = new byte[7];
            await ReadExactBytesAsync(_networkStream, header, 7);

            int remainingLength = (header[4] << 8) | header[5];
            byte[] body = new byte[remainingLength - 1]; // unitId already in header[6]
            await ReadExactBytesAsync(_networkStream, body, body.Length);
            _packetsReceived++;

            byte fc = body[0];
            if ((fc & 0x80) != 0)
            {
                byte errCode = body.Length > 1 ? body[1] : (byte)0;
                Log(LogLevel.Error, $"Modbus FC03 Exception: 0x{errCode:X2}");
                return null;
            }

            byte byteCount = body[1];
            ushort[] result = new ushort[count];
            for (int i = 0; i < count && (2 + i * 2 + 1) < body.Length; i++)
            {
                result[i] = (ushort)((body[2 + i * 2] << 8) | body[2 + i * 2 + 1]);
            }
            return result;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"ReadHoldingRegisters failed: {ex.Message}");
            return null;
        }
        finally
        {
            _socketLock.Release();
        }
    }

    public async Task<bool> WriteSingleRegisterAsync(int address, ushort value)
    {
        if (_config.IsSimulationMode)
        {
            _packetsSent++;
            _packetsReceived++;
            _simulator.WriteHoldingRegister(address, value);
            return true;
        }

        if (_networkStream == null || !_tcpClient!.Connected) return false;

        await _socketLock.WaitAsync();
        try
        {
            ushort transId = unchecked(++_transactionId);
            byte[] request = new byte[12];
            request[0] = (byte)(transId >> 8);
            request[1] = (byte)(transId & 0xFF);
            request[2] = 0x00;
            request[3] = 0x00;
            request[4] = 0x00;
            request[5] = 0x06;
            request[6] = _config.UnitId;
            request[7] = 0x06; // FC06 Write Single Register
            request[8] = (byte)(address >> 8);
            request[9] = (byte)(address & 0xFF);
            request[10] = (byte)(value >> 8);
            request[11] = (byte)(value & 0xFF);

            await _networkStream.WriteAsync(request, 0, request.Length);
            _packetsSent++;

            byte[] response = new byte[12];
            await ReadExactBytesAsync(_networkStream, response, 12);
            _packetsReceived++;

            return true;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"WriteSingleRegister failed: {ex.Message}");
            return false;
        }
        finally
        {
            _socketLock.Release();
        }
    }

    public async Task<bool> WriteMultipleRegistersAsync(int startAddress, ushort[] values)
    {
        if (_config.IsSimulationMode)
        {
            _packetsSent++;
            _packetsReceived++;
            _simulator.WriteHoldingRegisters(startAddress, values);
            return true;
        }

        if (_networkStream == null || !_tcpClient!.Connected) return false;

        await _socketLock.WaitAsync();
        try
        {
            ushort transId = unchecked(++_transactionId);
            int byteCount = values.Length * 2;
            int length = 7 + byteCount;
            byte[] request = new byte[6 + length];

            request[0] = (byte)(transId >> 8);
            request[1] = (byte)(transId & 0xFF);
            request[2] = 0x00;
            request[3] = 0x00;
            request[4] = (byte)(length >> 8);
            request[5] = (byte)(length & 0xFF);
            request[6] = _config.UnitId;
            request[7] = 0x10; // FC16 Write Multiple Registers
            request[8] = (byte)(startAddress >> 8);
            request[9] = (byte)(startAddress & 0xFF);
            request[10] = (byte)(values.Length >> 8);
            request[11] = (byte)(values.Length & 0xFF);
            request[12] = (byte)byteCount;

            for (int i = 0; i < values.Length; i++)
            {
                request[13 + i * 2] = (byte)(values[i] >> 8);
                request[13 + i * 2 + 1] = (byte)(values[i] & 0xFF);
            }

            await _networkStream.WriteAsync(request, 0, request.Length);
            _packetsSent++;

            byte[] response = new byte[12];
            await ReadExactBytesAsync(_networkStream, response, 12);
            _packetsReceived++;

            return true;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"WriteMultipleRegisters failed: {ex.Message}");
            return false;
        }
        finally
        {
            _socketLock.Release();
        }
    }

    public async Task<bool> WriteFloatRegistersAsync(int startAddress, float value, Endianness endianness)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);

        ushort highWord = (ushort)((bytes[3] << 8) | bytes[2]);
        ushort lowWord = (ushort)((bytes[1] << 8) | bytes[0]);

        ushort[] words = endianness switch
        {
            Endianness.LittleEndian_CDAB => [lowWord, highWord],
            _ => [highWord, lowWord]
        };

        return await WriteMultipleRegistersAsync(startAddress, words);
    }

    public async Task<float?> ReadFloatRegistersAsync(int startAddress, Endianness endianness)
    {
        var words = await ReadHoldingRegistersAsync(startAddress, 2);
        if (words == null || words.Length < 2) return null;

        ushort highWord = endianness == Endianness.LittleEndian_CDAB ? words[1] : words[0];
        ushort lowWord = endianness == Endianness.LittleEndian_CDAB ? words[0] : words[1];

        byte[] bytes = new byte[4];
        bytes[3] = (byte)(highWord >> 8);
        bytes[2] = (byte)(highWord & 0xFF);
        bytes[1] = (byte)(lowWord >> 8);
        bytes[0] = (byte)(lowWord & 0xFF);

        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    public async Task<bool[]?> ReadCoilsAsync(int startAddress, int count)
    {
        if (_config.IsSimulationMode)
        {
            return _simulator.ReadCoils(startAddress, count);
        }
        return null;
    }

    public async Task<bool> WriteSingleCoilAsync(int address, bool value)
    {
        if (_config.IsSimulationMode)
        {
            _simulator.WriteCoil(address, value);
            return true;
        }
        return false;
    }

    public async Task<bool> SendJointTargetsAsync(double q1, double q2, double q3, double q4)
    {
        // Write Float32 Q1..Q4 to Holding Registers 3, 5, 7, 9 (8 registers)
        ushort[] allWords = new ushort[8];
        PackFloatIntoWords((float)q1, _config.FloatEndianness, allWords, 0);
        PackFloatIntoWords((float)q2, _config.FloatEndianness, allWords, 2);
        PackFloatIntoWords((float)q3, _config.FloatEndianness, allWords, 4);
        PackFloatIntoWords((float)q4, _config.FloatEndianness, allWords, 6);

        bool success = await WriteMultipleRegistersAsync(3, allWords);
        if (success)
        {
            Log(LogLevel.Tx, $"Sent Joint Targets -> Q1: {q1:F1}°, Q2: {q2:F1}°, Q3: {q3:F1}°, Q4: {q4:F1}°");
        }
        return success;
    }

    public async Task<bool> SendCartesianTargetsAsync(double x, double y, double z, double pitch)
    {
        // Write Float32 X, Y, Z, Pitch to Holding Registers 19, 21, 23, 25 (8 registers)
        ushort[] allWords = new ushort[8];
        PackFloatIntoWords((float)x, _config.FloatEndianness, allWords, 0);
        PackFloatIntoWords((float)y, _config.FloatEndianness, allWords, 2);
        PackFloatIntoWords((float)z, _config.FloatEndianness, allWords, 4);
        PackFloatIntoWords((float)pitch, _config.FloatEndianness, allWords, 6);

        bool success = await WriteMultipleRegistersAsync(19, allWords);
        if (success)
        {
            Log(LogLevel.Tx, $"Sent Cartesian Targets -> X: {x:F1}, Y: {y:F1}, Z: {z:F1}, Pitch: {pitch:F1}°");
        }
        return success;
    }

    public async Task<bool> SendEmergencyStopAsync()
    {
        // Control Word Bit 2 = E-Stop (0x0004)
        bool success = await WriteSingleRegisterAsync(1, 0x0004);
        Log(LogLevel.Error, "EMERGENCY STOP (E-STOP) SENT TO OPTA PLC!");
        return success;
    }

    public async Task<bool> SendResetErrorAsync()
    {
        // Control Word Bit 4 = Reset Error (0x0010)
        bool success = await WriteSingleRegisterAsync(1, 0x0010);
        Log(LogLevel.Info, "Reset Error command sent to OPTA PLC.");
        return success;
    }

    public async Task<bool> SendGripperCommandAsync(bool close)
    {
        ushort val = close ? (ushort)1 : (ushort)0;
        bool success = await WriteSingleRegisterAsync(43, val);
        Log(LogLevel.Tx, $"Gripper command: {(close ? "CLOSE" : "OPEN")}");
        return success;
    }

    private static void PackFloatIntoWords(float value, Endianness endianness, ushort[] buffer, int offset)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);

        ushort highWord = (ushort)((bytes[3] << 8) | bytes[2]);
        ushort lowWord = (ushort)((bytes[1] << 8) | bytes[0]);

        if (endianness == Endianness.LittleEndian_CDAB)
        {
            buffer[offset] = lowWord;
            buffer[offset + 1] = highWord;
        }
        else
        {
            buffer[offset] = highWord;
            buffer[offset + 1] = lowWord;
        }
    }

    private static async Task ReadExactBytesAsync(Stream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead));
            if (read == 0) throw new IOException("Connection closed prematurely by remote host.");
            totalRead += read;
        }
    }

    private void Log(LogLevel level, string message, string hex = "")
    {
        LogReceived?.Invoke(new LogMessage
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            RawHex = hex
        });
    }

    public void Dispose()
    {
        StopPollingLoop();
        _simulator.Stop();
        _networkStream?.Dispose();
        _tcpClient?.Dispose();
        _socketLock.Dispose();
    }
}
