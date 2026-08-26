# MQTT Topics and JSON Data Schema Specification

- **Broker**: Local Mosquitto MQTT Broker (Default Port: 1883)
- **QoS Level**: 1 (At least once delivery)
- **Publisher/Subscriber**: Finder Opta (PLC Client) <---> WPF SCADA Application (C# Client)

---

## 1. Telemetry Topic (`robot/status/telemetry`)
Published by Finder Opta every 100ms or immediately upon status change.

```json
{
  "timestamp": "2026-08-18T14:30:00.000Z",
  "state": "IDLE",
  "joints": {
    "j1_angle": 0.0,
    "j2_angle": 90.0,
    "j3_angle": 0.0,
    "gripper_pos": 10
  },
  "pulses": {
    "m1": 0,
    "m2": 0,
    "m3": 0
  },
  "limits": {
    "l1_triggered": false,
    "l2_triggered": false,
    "l3_triggered": false
  },
  "homed": true,
  "error_code": 0
}
```

---

## 2. Command Topic (`robot/control/command`)
Published by WPF SCADA to trigger robot movements or mode changes.

```json
{
  "command": "MOVE",
  "targets": {
    "j1_angle": 45.0,
    "j2_angle": 60.0,
    "j3_angle": -15.0,
    "gripper_pos": 90
  },
  "jog": {
    "axis": "J1",
    "direction": "CW",
    "step": 5.0
  }
}
```

### Allowed `command` Values:
- `MOVE`: Moves joints to specified target angles (`j1_angle`, `j2_angle`, `j3_angle`, `gripper_pos`).
- `HOME`: Triggers homing calibration routine on FX3U for Joint 1, Joint 2, Joint 3.
- `STOP`: Immediate motion cancel.
- `ESTOP`: Emergency Stop - resets all Modbus outputs and enters fault state.
- `JOG`: Jogs specified axis (`J1`, `J2`, `J3`, `J4`) in `CW` or `CCW` direction by `step` degrees.
- `GRIPPER`: Sets gripper position (`0` to `180` degrees).
