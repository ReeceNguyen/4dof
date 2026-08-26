# FX3U-16MT/ES Ladder Logic & Modbus RTU Slave Setup Guide

## Hardware Wiring Configuration

- **PLC**: Mitsubishi FX3U-16MT/ES (Transistor Output NPN)
- **Modbus Adapter**: FX3U-485-BD or FX3U-485ADP-MB board
- **Inputs**:
  - `X0`: Micro Switch 024-A (Joint 1 Base Limit Switch, NC or NO configured in ladder)
  - `X1`: Micro Switch 024-A (Joint 2 Shoulder Limit Switch)
  - `X2`: Micro Switch 024-A (Joint 3 Elbow Limit Switch)
- **Outputs (Pulse & Direction to DM542 Drivers)**:
  - `Y0`: Motor 1 Pulse (`PUL1`)
  - `Y1`: Motor 2 Pulse (`PUL2`)
  - `Y2`: Motor 3 Pulse (`PUL3`)
  - `Y3`: Motor 1 Direction (`DIR1`)
  - `Y4`: Motor 2 Direction (`DIR2`)
  - `Y5`: Motor 3 Direction (`DIR3`)

---

## Modbus RTU Slave Register Map

- **Communication Channel**: Ch 1 (BD Board) / Ch 2 (ADP Board)
- **Serial Settings**: 19200 Baud, 8 Data Bits, No Parity, 1 Stop Bit
- **Slave Station ID**: `1`
- **D8120 Channel Setting**: `0x0C87` (19200 bps, 8-N-1, RS485 Modbus RTU Slave)

---

## GX Works2 Instruction / Ladder Code Structure

```mne
;===============================================================
; 1. MODBUS RTU SLAVE COMMUNICATION INITIALIZATION
;===============================================================
LD M8002                    ; First Scan Pulse
MOV H0C87 D8120             ; Set RS485 communication: 19200 bps, 8-N-1, Modbus RTU
MOV K1 D8121                ; Set Modbus Slave Station ID = 1
MOV K1000 D8129             ; Set Modbus Timeout = 1000ms

;===============================================================
; 2. INPUT STATUS MAPPING TO REGISTER D100 (MODBUS REG 40007)
;===============================================================
LD X0                       ; Joint 1 Limit Switch
OUT M10                     ; Internal Bit 0
LD X1                       ; Joint 2 Limit Switch
OUT M11                     ; Internal Bit 1
LD X2                       ; Joint 3 Limit Switch
OUT M12                     ; Internal Bit 2

LD M8000                    ; Always ON
MOV K1M10 D100              ; Copy M10-M13 into lower bits of D100 register

;===============================================================
; 3. PULSE POSITION FEEDBACK REGISTERS (D8140, D8142, D8144)
;===============================================================
; FX3U automatically keeps track of pulse outputs:
; D8140 (32-bit): Y0 (Motor 1) Accumulator -> Maps to Modbus 40001-40002
; D8142 (32-bit): Y1 (Motor 2) Accumulator -> Maps to Modbus 40003-40004
; D8144 (32-bit): Y2 (Motor 3) Accumulator -> Maps to Modbus 40005-40006

;===============================================================
; 4. SAFETY INTERLOCKS & HARDWARE LIMIT STOP
;===============================================================
; Immediately clear positioning flags if limit switches are struck
LD X0
RST M100                    ; Cancel Motor 1 command

LD X1
RST M101                    ; Cancel Motor 2 command

LD X2
RST M102                    ; Cancel Motor 3 command

;===============================================================
; 5. MOTOR POSITIONING COMMANDS (DRVI - Relative Drive)
;===============================================================
; Motor 1 (Joint 1 - Base): Pulses from D200/D201, Freq from D206
LD D209.0                   ; Bit 0 of Control Register D209
ANI X0                      ; Interlocked with Limit Switch X0
DDRVI D200 D206 Y0 Y3       ; Output relative pulses on Y0, direction on Y3

; Motor 2 (Joint 2 - Shoulder): Pulses from D202/D203, Freq from D207
LD D209.1                   ; Bit 1 of Control Register D209
ANI X1                      ; Interlocked with Limit Switch X1
DDRVI D202 D207 Y1 Y4       ; Output relative pulses on Y1, direction on Y4

; Motor 3 (Joint 3 - Elbow): Pulses from D204/D205, Freq from D208
LD D209.2                   ; Bit 2 of Control Register D209
ANI X2                      ; Interlocked with Limit Switch X2
DDRVI D204 D208 Y2 Y5       ; Output relative pulses on Y2, direction on Y5

;===============================================================
; 6. HOMING SUBROUTINE (Triggered via D209 Bit 3)
;===============================================================
; Homing Motor 1: Jog CCW until X0 limit switch triggers, then zero counter D8140
LD D209.3
ANI X0
DDRVI K-100000 D206 Y0 Y3   ; Drive slowly CCW towards limit switch

LD D209.3
AND X0                      ; When Limit Switch X0 is struck during Homing
DMOV K0 D8140               ; Clear Y0 Pulse Count to 0
RST D209.3                  ; Reset Homing Flag
SET M13                     ; Set Homing Complete Flag in status D100
