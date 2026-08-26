PROGRAM MAIN
VAR
    // System State
    eState              : E_SystemState := E_SystemState.STATE_INIT;
    stTelemetry         : ST_RobotTelemetry;

    // Modbus RTU Slave 1 (FX3U) Registers - Mapped to Hardware I/O / Modbus Channel
    wFX3U_Status        : WORD;               (* Modbus Read: Reg 40007 *)
    diFX3U_PosM1        : DINT;               (* Modbus Read: Reg 40001-40002 *)
    diFX3U_PosM2        : DINT;               (* Modbus Read: Reg 40003-40004 *)
    diFX3U_PosM3        : DINT;               (* Modbus Read: Reg 40005-40006 *)

    diFX3U_TargetM1     : DINT;               (* Modbus Write: Reg 40010-40011 *)
    diFX3U_TargetM2     : DINT;               (* Modbus Write: Reg 40012-40013 *)
    diFX3U_TargetM3     : DINT;               (* Modbus Write: Reg 40014-40015 *)
    uFX3U_SpeedM1       : UINT := 2000;       (* Modbus Write: Reg 40016 *)
    uFX3U_SpeedM2       : UINT := 2000;       (* Modbus Write: Reg 40017 *)
    uFX3U_SpeedM3       : UINT := 2000;       (* Modbus Write: Reg 40018 *)
    wFX3U_Control       : WORD := 0;          (* Modbus Write: Reg 40019 *)

    // Modbus RTU Slave 2 (Arduino Uno) Registers
    uUno_TargetServo    : UINT := 10;         (* Modbus Write: Reg 40001 *)
    uUno_ServoAttach    : UINT := 1;          (* Modbus Write: Reg 40002 *)
    uUno_CurrentServo   : UINT;               (* Modbus Read: Reg 30001 *)

    // Mechanical Configuration Constants
    PULSES_PER_DEGREE_J1 : REAL := 88.8888;   (* DM542 1600 microsteps / 360 deg * 20:1 gear *)
    PULSES_PER_DEGREE_J2 : REAL := 88.8888;
    PULSES_PER_DEGREE_J3 : REAL := 88.8888;

    // Command Target Values received from MQTT
    fTargetJ1           : REAL := 0.0;
    fTargetJ2           : REAL := 90.0;
    fTargetJ3           : REAL := 0.0;
    uTargetGripper      : UINT := 10;

    // Commands Flags from SCADA / MQTT Interface
    bCmdMove            : BOOL := FALSE;
    bCmdHome            : BOOL := FALSE;
    bCmdStop            : BOOL := FALSE;
    bCmdEStop           : BOOL := FALSE;

    // Timers & Helper Variables
    tHomingTimer        : TON;
    bHomed              : BOOL := FALSE;
END_VAR

//=============================================================================
// MAIN PROGRAM EXECUTION (IEC 61131-3 STRUCTURED TEXT)
//=============================================================================

// 1. Process Hardware Status Bits from FX3U Modbus Register
stTelemetry.bLimit1 := wFX3U_Status.0;
stTelemetry.bLimit2 := wFX3U_Status.1;
stTelemetry.bLimit3 := wFX3U_Status.2;
stTelemetry.bHomed  := wFX3U_Status.3;

// Convert FX3U pulse position feedback to joint angles in degrees
stTelemetry.diPulsesM1 := diFX3U_PosM1;
stTelemetry.diPulsesM2 := diFX3U_PosM2;
stTelemetry.diPulsesM3 := diFX3U_PosM3;

stTelemetry.fJ1Angle := DINT_TO_REAL(diFX3U_PosM1) / PULSES_PER_DEGREE_J1;
stTelemetry.fJ2Angle := DINT_TO_REAL(diFX3U_PosM2) / PULSES_PER_DEGREE_J2;
stTelemetry.fJ3Angle := DINT_TO_REAL(diFX3U_PosM3) / PULSES_PER_DEGREE_J3;
stTelemetry.uGripperPos := uUno_CurrentServo;

// 2. Global Emergency Stop Check
IF bCmdEStop THEN
    eState := E_SystemState.STATE_ESTOP;
END_IF

// 3. System State Machine Execution
CASE eState OF
    E_SystemState.STATE_INIT:
        wFX3U_Control := 0;
        uUno_TargetServo := 10; // Open gripper
        IF stTelemetry.bHomed THEN
            eState := E_SystemState.STATE_IDLE;
        ELSE
            eState := E_SystemState.STATE_UNHOMED;
        END_IF;

    E_SystemState.STATE_UNHOMED:
        IF bCmdHome THEN
            bCmdHome := FALSE;
            eState := E_SystemState.STATE_HOMING;
        END_IF;

    E_SystemState.STATE_HOMING:
        wFX3U_Control.3 := TRUE; // Set Homing Bit on FX3U
        tHomingTimer(IN := TRUE, PT := T#15S);

        IF stTelemetry.bHomed THEN
            wFX3U_Control.3 := FALSE;
            tHomingTimer(IN := FALSE);
            bHomed := TRUE;
            eState := E_SystemState.STATE_IDLE;
        ELSIF tHomingTimer.Q THEN
            wFX3U_Control.3 := FALSE;
            tHomingTimer(IN := FALSE);
            stTelemetry.uErrorCode := 101; // Homing Timeout Error
            eState := E_SystemState.STATE_ERROR;
        END_IF;

    E_SystemState.STATE_IDLE:
        wFX3U_Control.0 := FALSE;
        wFX3U_Control.1 := FALSE;
        wFX3U_Control.2 := FALSE;

        IF bCmdMove THEN
            bCmdMove := FALSE;

            // Convert Joint Angle Targets (deg) to Pulse Counts for FX3U
            diFX3U_TargetM1 := REAL_TO_DINT(fTargetJ1 * PULSES_PER_DEGREE_J1);
            diFX3U_TargetM2 := REAL_TO_DINT(fTargetJ2 * PULSES_PER_DEGREE_J2);
            diFX3U_TargetM3 := REAL_TO_DINT(fTargetJ3 * PULSES_PER_DEGREE_J3);
            uUno_TargetServo := uTargetGripper;

            // Trigger Movement
            wFX3U_Control.0 := TRUE; // Move M1
            wFX3U_Control.1 := TRUE; // Move M2
            wFX3U_Control.2 := TRUE; // Move M3

            eState := E_SystemState.STATE_RUNNING;
        ELSIF bCmdHome THEN
            bCmdHome := FALSE;
            eState := E_SystemState.STATE_HOMING;
        END_IF;

    E_SystemState.STATE_RUNNING:
        IF bCmdStop THEN
            bCmdStop := FALSE;
            wFX3U_Control.0 := FALSE;
            wFX3U_Control.1 := FALSE;
            wFX3U_Control.2 := FALSE;
            eState := E_SystemState.STATE_IDLE;
        ELSIF (diFX3U_PosM1 = diFX3U_TargetM1) AND
              (diFX3U_PosM2 = diFX3U_TargetM2) AND
              (diFX3U_PosM3 = diFX3U_TargetM3) THEN
            // Motion Completed
            wFX3U_Control.0 := FALSE;
            wFX3U_Control.1 := FALSE;
            wFX3U_Control.2 := FALSE;
            eState := E_SystemState.STATE_IDLE;
        END_IF;

    E_SystemState.STATE_ERROR:
        wFX3U_Control := 0;
        IF bCmdStop THEN
            bCmdStop := FALSE;
            stTelemetry.uErrorCode := 0;
            eState := E_SystemState.STATE_UNHOMED;
        END_IF;

    E_SystemState.STATE_ESTOP:
        wFX3U_Control := 0;
        uUno_ServoAttach := 0; // Detach SG90 servo
        IF NOT bCmdEStop THEN
            uUno_ServoAttach := 1;
            eState := E_SystemState.STATE_UNHOMED;
        END_IF;
END_CASE;

stTelemetry.eState := eState;
