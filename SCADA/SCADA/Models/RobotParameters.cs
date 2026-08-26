using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SCADA.Models;

/// <summary>
/// Model representing the physical parameters and state of a 4-DOF articulated robot arm.
/// Link 1: Base to Shoulder (Z offset)
/// Link 2: Upper Arm (Shoulder to Elbow)
/// Link 3: Forearm (Elbow to Wrist)
/// Link 4: End Effector / Tool (Wrist to Tool tip)
/// </summary>
public partial class RobotParameters : ObservableObject
{
    // Link Lengths (in mm)
    [ObservableProperty] private double _l1 = 150.0; // Base height (d1)
    [ObservableProperty] private double _l2 = 200.0; // Upper arm length (a2)
    [ObservableProperty] private double _l3 = 180.0; // Forearm length (a3)
    [ObservableProperty] private double _l4 = 120.0; // End-effector length (a4)

    // Link Masses (in kg) for Euler-Lagrange Dynamics
    [ObservableProperty] private double _m1 = 1.5; // Base link mass
    [ObservableProperty] private double _m2 = 2.0; // Link 2 mass
    [ObservableProperty] private double _m3 = 1.2; // Link 3 mass
    [ObservableProperty] private double _m4 = 0.6; // Link 4 + gripper payload mass

    // Centers of mass (distance from joint along link, mm)
    [ObservableProperty] private double _rc1 = 75.0;
    [ObservableProperty] private double _rc2 = 100.0;
    [ObservableProperty] private double _rc3 = 90.0;
    [ObservableProperty] private double _rc4 = 60.0;

    // Joint Angles (in Degrees)
    [ObservableProperty] private double _q1 = 0.0;  // Base Yaw (-180° to +180°)
    [ObservableProperty] private double _q2 = 45.0; // Shoulder Pitch (-90° to +135°)
    [ObservableProperty] private double _q3 = -45.0;// Elbow Pitch (-150° to +150°)
    [ObservableProperty] private double _q4 = 0.0;  // Wrist Pitch (-180° to +180°)

    // Target Joint Angles (in Degrees)
    [ObservableProperty] private double _targetQ1 = 0.0;
    [ObservableProperty] private double _targetQ2 = 45.0;
    [ObservableProperty] private double _targetQ3 = -45.0;
    [ObservableProperty] private double _targetQ4 = 0.0;

    // Joint Angular Velocities (deg/s)
    [ObservableProperty] private double _q1Dot = 0.0;
    [ObservableProperty] private double _q2Dot = 0.0;
    [ObservableProperty] private double _q3Dot = 0.0;
    [ObservableProperty] private double _q4Dot = 0.0;

    // Joint Angular Accelerations (deg/s^2)
    [ObservableProperty] private double _q1Ddot = 0.0;
    [ObservableProperty] private double _q2Ddot = 0.0;
    [ObservableProperty] private double _q3Ddot = 0.0;
    [ObservableProperty] private double _q4Ddot = 0.0;

    // Calculated Joint Torques (N*m) from Dynamics
    [ObservableProperty] private double _tau1 = 0.0;
    [ObservableProperty] private double _tau2 = 0.0;
    [ObservableProperty] private double _tau3 = 0.0;
    [ObservableProperty] private double _tau4 = 0.0;
    [ObservableProperty] private double _totalPowerWatts = 0.0;

    // Cartesian Coordinates (mm) and Orientation (degrees)
    [ObservableProperty] private double _x = 0.0;
    [ObservableProperty] private double _y = 0.0;
    [ObservableProperty] private double _z = 0.0;
    [ObservableProperty] private double _pitch = 0.0; // Wrist pitch angle relative to horizontal
    [ObservableProperty] private double _yaw = 0.0;   // Base yaw angle
    [ObservableProperty] private double _roll = 0.0;

    // Target Cartesian Coordinates
    [ObservableProperty] private double _targetX = 250.0;
    [ObservableProperty] private double _targetY = 0.0;
    [ObservableProperty] private double _targetZ = 200.0;
    [ObservableProperty] private double _targetPitch = 0.0;

    // Joint Limits (Degrees)
    [ObservableProperty] private double _q1Min = -180.0;
    [ObservableProperty] private double _q1Max = 180.0;
    [ObservableProperty] private double _q2Min = -90.0;
    [ObservableProperty] private double _q2Max = 135.0;
    [ObservableProperty] private double _q3Min = -150.0;
    [ObservableProperty] private double _q3Max = 150.0;
    [ObservableProperty] private double _q4Min = -180.0;
    [ObservableProperty] private double _q4Max = 180.0;

    // 3D Joint Positions in Base Coordinate Frame {0} (for visualization)
    public (double X, double Y, double Z) BasePos { get; set; } = (0, 0, 0);
    public (double X, double Y, double Z) Joint1Pos { get; set; } = (0, 0, 150);
    public (double X, double Y, double Z) Joint2Pos { get; set; } = (0, 0, 150);
    public (double X, double Y, double Z) Joint3Pos { get; set; } = (0, 0, 150);
    public (double X, double Y, double Z) EndEffectorPos { get; set; } = (0, 0, 150);

    // Target 3D Joint Positions (for ghost arm visualizer)
    public (double X, double Y, double Z) TargetJoint1Pos { get; set; } = (0, 0, 150);
    public (double X, double Y, double Z) TargetJoint2Pos { get; set; } = (0, 0, 150);
    public (double X, double Y, double Z) TargetJoint3Pos { get; set; } = (0, 0, 150);
    public (double X, double Y, double Z) TargetEndEffectorPos { get; set; } = (0, 0, 150);

    // Status flags
    [ObservableProperty] private bool _isReachable = true;
    [ObservableProperty] private bool _isSingular = false;
    [ObservableProperty] private double _manipulabilityIndex = 0.0;
    [ObservableProperty] private bool _isGripperClosed = false;

    public void CopyJointsToTarget()
    {
        TargetQ1 = Q1;
        TargetQ2 = Q2;
        TargetQ3 = Q3;
        TargetQ4 = Q4;
    }

    public void CopyCartesianToTarget()
    {
        TargetX = X;
        TargetY = Y;
        TargetZ = Z;
        TargetPitch = Pitch;
    }
}

/// <summary>
/// Row item for Denavit-Hartenberg parameter table display.
/// </summary>
public class DhParameterRow
{
    public int LinkIndex { get; set; }
    public string JointName { get; set; } = string.Empty;
    public double ThetaDeg { get; set; }
    public double D { get; set; }
    public double A { get; set; }
    public double AlphaDeg { get; set; }
    public string TransformationMatrixFormatted { get; set; } = string.Empty;
}
