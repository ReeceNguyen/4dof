/*
 * Uno_EndEffector.ino
 *
 * Arduino Uno Modbus RTU Slave (Slave ID 2) for Controlling SG90 Servo End Effector
 *
 * Hardware Wiring:
 * - RS485 MAX485 Transceiver:
 *   - DI -> Arduino Pin 1 (TX)
 *   - RO -> Arduino Pin 0 (RX)
 *   - DE & RE (Tied together) -> Arduino Pin 2 (Direction Control Pin)
 *   - VCC -> 5V, GND -> GND
 * - SG90 Micro Servo:
 *   - Signal Pin -> Arduino Pin 9 (PWM)
 *   - VCC -> External 5V Power Supply (Common GND with Arduino)
 *   - GND -> Common GND
 *
 * Modbus Register Mapping (Slave ID 2):
 * - Holding Register 40001 (0x0000): Target Servo Angle (0 to 180 degrees)
 * - Holding Register 40002 (0x0001): Servo Power/Attach State (0 = Detach, 1 = Attach)
 * - Input Register 30001 (0x0000): Current Servo Angle Feedback (0 to 180)
 * - Input Register 30002 (0x0001): Servo Status Flags (Bit 0: Attached)
 */

#include <Servo.h>
#include <ModbusRTUSlave.h>

// Pin Definitions
const uint8_t MAX485_DE_RE_PIN = 2;
const uint8_t SERVO_PIN = 9;

// Modbus Baudrate & Slave Configuration
const uint32_t BAUDRATE = 19200;
const uint8_t SLAVE_ID = 2;

// Modbus Data Buffers
uint16_t holdingRegisters[2]; // [0]: Target Angle, [1]: Attach State
uint16_t inputRegisters[2];   // [0]: Current Angle, [1]: Status Flags

// Instantiate Modbus Slave and Servo Objects
ModbusRTUSlave modbus(Serial, MAX485_DE_RE_PIN);
Servo sg90Servo;

// Internal Variables
uint16_t lastAngle = 999;
bool isAttached = false;

void setup() {
  // Initialize Holding Registers with default safe values
  holdingRegisters[0] = 10; // Default open angle (10 degrees)
  holdingRegisters[1] = 1;  // Default attached (1)

  // Configure Modbus RTU Slave
  modbus.configureHoldingRegisters(holdingRegisters, 2);
  modbus.configureInputRegisters(inputRegisters, 2);
  modbus.begin(SLAVE_ID, BAUDRATE);

  // Attach Servo
  sg90Servo.attach(SERVO_PIN);
  sg90Servo.write(holdingRegisters[0]);
  isAttached = true;
}

void loop() {
  // Poll Modbus requests continuously
  modbus.poll();

  // Process Servo Attach / Detach state change
  uint16_t requestedAttachState = holdingRegisters[1];
  if (requestedAttachState == 1 && !isAttached) {
    sg90Servo.attach(SERVO_PIN);
    isAttached = true;
  } else if (requestedAttachState == 0 && isAttached) {
    sg90Servo.detach();
    isAttached = false;
  }

  // Process Servo Position update if attached
  if (isAttached) {
    uint16_t targetAngle = holdingRegisters[0];

    // Constrain angle strictly within safe servo limits (0 to 180 degrees)
    if (targetAngle > 180) {
      targetAngle = 180;
    }

    if (targetAngle != lastAngle) {
      sg90Servo.write(targetAngle);
      lastAngle = targetAngle;
    }
  }

  // Update Input Feedback Registers for Modbus Master polling
  inputRegisters[0] = lastAngle;
  inputRegisters[1] = isAttached ? 1 : 0;
}
