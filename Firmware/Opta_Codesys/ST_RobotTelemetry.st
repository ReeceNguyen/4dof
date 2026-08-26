TYPE ST_RobotTelemetry :
STRUCT
    eState          : E_SystemState;
    fJ1Angle        : REAL;
    fJ2Angle        : REAL;
    fJ3Angle        : REAL;
    uGripperPos     : UINT;
    diPulsesM1      : DINT;
    diPulsesM2      : DINT;
    diPulsesM3      : DINT;
    bLimit1         : BOOL;
    bLimit2         : BOOL;
    bLimit3         : BOOL;
    bHomed          : BOOL;
    uErrorCode      : UINT;
END_STRUCT
END_TYPE
