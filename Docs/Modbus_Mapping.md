# Modbus RTU Register Mapping Specification

- **Master**: Finder Opta (RS485, Port settings: 19200 Baud, 8 Data Bits, No Parity, 1 Stop Bit)
- **Slave ID 1**: Mitsubishi FX3U-16MT/ES (Main 3DOF Arm & Limit Switches)
- **Slave ID 2**: Arduino Uno (End Effector SG90 Servo Controller)

---

## 1. Mitsubishi FX3U-16MT/ES (Slave ID 1)

### Input / Status Registers (Read-Only by Finder Opta - Function Code 04)

| Register Address | PLC Memory | Data Type | Description |
| :--- | :--- | :--- | :--- |
| `40001` (0x0000) | `D8140` (Low) | INT16 / DWORD | Motor 1 (Joint 1 Base) Current Pulse Position (Low 16-bit) |
| `40002` (0x0001) | `D8141` (High) | INT16 / DWORD | Motor 1 (Joint 1 Base) Current Pulse Position (High 16-bit) |
| `40003` (0x0002) | `D8142` (Low) | INT16 / DWORD | Motor 2 (Joint 2 Shoulder) Current Pulse Position (Low 16-bit) |
| `40004` (0x0003) | `D8143` (High) | INT16 / DWORD | Motor 2 (Joint 2 Shoulder) Current Pulse Position (High 16-bit) |
| `40005` (0x0004) | `D8144` (Low) | INT16 / DWORD | Motor 3 (Joint 3 Elbow) Current Pulse Position (Low 16-bit) |
| `40006` (0x0005) | `D8145` (High) | INT16 / DWORD | Motor 3 (Joint 3 Elbow) Current Pulse Position (High 16-bit) |
| `40007` (0x0006) | `D100` | WORD | Status Bitmask:<br>- Bit 0: X0 Limit Joint 1 (1 = Active)<br>- Bit 1: X1 Limit Joint 2 (1 = Active)<br>- Bit 2: X2 Limit Joint 3 (1 = Active)<br>- Bit 3: Homing Completed (1 = Yes)<br>- Bit 4: Motor 1 Busy (1 = Moving)<br>- Bit 5: Motor 2 Busy (1 = Moving)<br>- Bit 6: Motor 3 Busy (1 = Moving)<br>- Bit 7: Error Fault Flag |

### Holding Registers (Writable by Finder Opta - Function Code 06 / 16)

| Register Address | PLC Memory | Data Type | Description |
| :--- | :--- | :--- | :--- |
| `40010` (0x0009) | `D200` (Low) | INT16 / DWORD | Target Pulse Count - Motor 1 (Low 16-bit) |
| `40011` (0x000A) | `D201` (High) | INT16 / DWORD | Target Pulse Count - Motor 1 (High 16-bit) |
| `40012` (0x000B) | `D202` (Low) | INT16 / DWORD | Target Pulse Count - Motor 2 (Low 16-bit) |
| `40013` (0x000C) | `D203` (High) | INT16 / DWORD | Target Pulse Count - Motor 2 (High 16-bit) |
| `40014` (0x000D) | `D204` (Low) | INT16 / DWORD | Target Pulse Count - Motor 3 (Low 16-bit) |
| `40015` (0x000E) | `D205` (High) | INT16 / DWORD | Target Pulse Count - Motor 3 (High 16-bit) |
| `40016` (0x000F) | `D206` | UINT16 | Speed Frequency - Motor 1 (Hz, 100 - 20,000 Hz) |
| `40017` (0x0010) | `D207` | UINT16 | Speed Frequency - Motor 2 (Hz, 100 - 20,000 Hz) |
| `40018` (0x0011) | `D208` | UINT16 | Speed Frequency - Motor 3 (Hz, 100 - 20,000 Hz) |
| `40019` (0x0012) | `D209` | WORD | Control Bitmask:<br>- Bit 0: Execute Move Motor 1<br>- Bit 1: Execute Move Motor 2<br>- Bit 2: Execute Move Motor 3<br>- Bit 3: Trigger Homing Sequence<br>- Bit 4: Reset Fault / Stop All Movements |

---

## 2. Arduino Uno (Slave ID 2 - SG90 End Effector)

### Holding Registers (Writable by Finder Opta - Function Code 06 / 16)

| Register Address | Variable | Data Type | Range / Description |
| :--- | :--- | :--- | :--- |
| `40001` (0x0000) | `targetServoAngle` | UINT16 | Target Angle: `0` to `180` degrees (`10` = Open, `120` = Clamped) |
| `40002` (0x0001) | `servoState` | UINT16 | `0` = Detach/Power Idle, `1` = Attach/Active Hold |

### Input Registers (Read-Only by Finder Opta - Function Code 04)

| Register Address | Variable | Data Type | Range / Description |
| :--- | :--- | :--- | :--- |
| `30001` (0x0000) | `currentServoAngle` | UINT16 | Feedback / Echoed Servo Angle (0-180) |
| `30002` (0x0001) | `statusFlags` | UINT16 | Bit 0: Servo Attached |
