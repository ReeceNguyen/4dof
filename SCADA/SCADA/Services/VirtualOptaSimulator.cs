using System;
using System.Threading;
using System.Threading.Tasks;
using SCADA.Models;

namespace SCADA.Services;

/// <summary>
/// Virtual Arduino OPTA Codesys PLC simulator running in-memory or on local socket.
/// Simulates Modbus register table, heartbeat watchdog echo, and physical arm motor dynamics.
/// </summary>
public class VirtualOptaSimulator
{
    private readonly ushort[] _holdingRegisters = new ushort[100];
    private readonly bool[] _coils = new bool[64];
    private readonly object _lock = new();

    private double _actualQ1 = 0.0;
    private double _actualQ2 = 45.0;
    private double _actualQ3 = -45.0;
    private double _actualQ4 = 0.0;

    private double _targetQ1 = 0.0;
    private double _targetQ2 = 45.0;
    private double _targetQ3 = -45.0;
    private double _targetQ4 = 0.0;

    private CancellationTokenSource? _simCts;
    private Task? _simLoopTask;

    public bool IsRunning => _simLoopTask != null && !_simLoopTask.IsCompleted;

    public VirtualOptaSimulator()
    {
        InitializeRegisters();
    }

    private void InitializeRegisters()
    {
        lock (_lock)
        {
            // Status Word = Ready (Bit 0) | Homed (Bit 4) | Watchdog OK (Bit 5)
            _holdingRegisters[2] = 0x0031;

            WriteFloatToRegisters(3, (float)_targetQ1);
            WriteFloatToRegisters(5, (float)_targetQ2);
            WriteFloatToRegisters(7, (float)_targetQ3);
            WriteFloatToRegisters(9, (float)_targetQ4);

            WriteFloatToRegisters(11, (float)_actualQ1);
            WriteFloatToRegisters(13, (float)_actualQ2);
            WriteFloatToRegisters(15, (float)_actualQ3);
            WriteFloatToRegisters(17, (float)_actualQ4);

            // Default Cartesian
            WriteFloatToRegisters(19, 250.0f); // Target X
            WriteFloatToRegisters(21, 0.0f);   // Target Y
            WriteFloatToRegisters(23, 200.0f); // Target Z
            WriteFloatToRegisters(25, 0.0f);   // Target Pitch

            WriteFloatToRegisters(27, 250.0f); // Actual X
            WriteFloatToRegisters(29, 0.0f);   // Actual Y
            WriteFloatToRegisters(31, 200.0f); // Actual Z
            WriteFloatToRegisters(33, 0.0f);   // Actual Pitch
        }
    }

    public void Start()
    {
        if (IsRunning) return;
        _simCts = new CancellationTokenSource();
        _simLoopTask = Task.Run(() => PhysicsLoop(_simCts.Token));
    }

    public void Stop()
    {
        _simCts?.Cancel();
        _simLoopTask = null;
    }

    private async Task PhysicsLoop(CancellationToken token)
    {
        const double dt = 0.02; // 20ms simulation step (50 Hz)
        const double maxSpeedDegPerSec = 90.0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                lock (_lock)
                {
                    // Read target angles from registers (offset 3, 5, 7, 9)
                    _targetQ1 = ReadFloatFromRegisters(3);
                    _targetQ2 = ReadFloatFromRegisters(5);
                    _targetQ3 = ReadFloatFromRegisters(7);
                    _targetQ4 = ReadFloatFromRegisters(9);

                    // Move actual angles smoothly towards target
                    double step = maxSpeedDegPerSec * dt;
                    _actualQ1 = MoveTowards(_actualQ1, _targetQ1, step);
                    _actualQ2 = MoveTowards(_actualQ2, _targetQ2, step);
                    _actualQ3 = MoveTowards(_actualQ3, _targetQ3, step);
                    _actualQ4 = MoveTowards(_actualQ4, _targetQ4, step);

                    // Write feedback actual angles to registers (offset 11, 13, 15, 17)
                    WriteFloatToRegisters(11, (float)_actualQ1);
                    WriteFloatToRegisters(13, (float)_actualQ2);
                    WriteFloatToRegisters(15, (float)_actualQ3);
                    WriteFloatToRegisters(17, (float)_actualQ4);

                    // Determine if moving
                    bool isMoving = Math.Abs(_actualQ1 - _targetQ1) > 0.05 ||
                                    Math.Abs(_actualQ2 - _targetQ2) > 0.05 ||
                                    Math.Abs(_actualQ3 - _targetQ3) > 0.05 ||
                                    Math.Abs(_actualQ4 - _targetQ4) > 0.05;

                    ushort status = 0x0001; // Bit 0: Ready
                    if (isMoving) status |= 0x0002; // Bit 1: Moving
                    else status |= 0x0004; // Bit 2: In Position
                    status |= 0x0010; // Bit 4: Homed
                    status |= 0x0020; // Bit 5: Watchdog OK

                    _holdingRegisters[2] = status;

                    // Approximate torques
                    float t1 = (float)(0.2 * Math.Sin(_actualQ1 * Math.PI / 180));
                    float t2 = (float)(4.5 * Math.Cos(_actualQ2 * Math.PI / 180) + 2.0 * Math.Cos((_actualQ2 + _actualQ3) * Math.PI / 180));
                    float t3 = (float)(2.0 * Math.Cos((_actualQ2 + _actualQ3) * Math.PI / 180));
                    float t4 = (float)(0.5 * Math.Cos((_actualQ2 + _actualQ3 + _actualQ4) * Math.PI / 180));

                    WriteFloatToRegisters(35, t1);
                    WriteFloatToRegisters(37, t2);
                    WriteFloatToRegisters(39, t3);
                    WriteFloatToRegisters(41, t4);
                }

                await Task.Delay(20, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore simulation loop errors
            }
        }
    }

    private static double MoveTowards(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta) return target;
        return current + Math.Sign(target - current) * maxDelta;
    }

    public ushort[] ReadHoldingRegisters(int startAddress, int count)
    {
        lock (_lock)
        {
            ushort[] result = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                int addr = startAddress + i;
                result[i] = (addr >= 0 && addr < _holdingRegisters.Length) ? _holdingRegisters[addr] : (ushort)0;
            }
            return result;
        }
    }

    public void WriteHoldingRegister(int address, ushort value)
    {
        lock (_lock)
        {
            if (address >= 0 && address < _holdingRegisters.Length)
            {
                _holdingRegisters[address] = value;
            }
        }
    }

    public void WriteHoldingRegisters(int startAddress, ushort[] values)
    {
        lock (_lock)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int addr = startAddress + i;
                if (addr >= 0 && addr < _holdingRegisters.Length)
                {
                    _holdingRegisters[addr] = values[i];
                }
            }
        }
    }

    public bool[] ReadCoils(int startAddress, int count)
    {
        lock (_lock)
        {
            bool[] result = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int addr = startAddress + i;
                result[i] = (addr >= 0 && addr < _coils.Length) && _coils[addr];
            }
            return result;
        }
    }

    public void WriteCoil(int address, bool value)
    {
        lock (_lock)
        {
            if (address >= 0 && address < _coils.Length)
            {
                _coils[address] = value;
            }
        }
    }

    private void WriteFloatToRegisters(int offset, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);

        // Big-endian word order (ABCD)
        ushort highWord = (ushort)((bytes[3] << 8) | bytes[2]);
        ushort lowWord = (ushort)((bytes[1] << 8) | bytes[0]);

        if (offset < _holdingRegisters.Length) _holdingRegisters[offset] = highWord;
        if (offset + 1 < _holdingRegisters.Length) _holdingRegisters[offset + 1] = lowWord;
    }

    private float ReadFloatFromRegisters(int offset)
    {
        if (offset + 1 >= _holdingRegisters.Length) return 0f;
        ushort highWord = _holdingRegisters[offset];
        ushort lowWord = _holdingRegisters[offset + 1];

        byte[] bytes = new byte[4];
        bytes[3] = (byte)(highWord >> 8);
        bytes[2] = (byte)(highWord & 0xFF);
        bytes[1] = (byte)(lowWord >> 8);
        bytes[0] = (byte)(lowWord & 0xFF);

        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }
}
