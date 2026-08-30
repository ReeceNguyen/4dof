using CommunityToolkit.Mvvm.ComponentModel;

namespace MobusTCP.Models;

public enum TrajectoryProfileType
{
    QuinticPolynomial,   // 5th order polynomial (smooth jerk-free)
    CubicPolynomial,     // 3rd order polynomial
    TrapezoidalVelocity, // LSPB (Linear Segment with Parabolic Blends)
    LinearCartesian      // Straight-line interpolation in Cartesian space with continuous IK
}

public partial class TrajectoryConfig : ObservableObject
{
    [ObservableProperty] private TrajectoryProfileType _profileType = TrajectoryProfileType.QuinticPolynomial;
    [ObservableProperty] private double _duration = 3.0; // Duration in seconds
    [ObservableProperty] private double _maxVelocity = 90.0; // deg/s or mm/s
    [ObservableProperty] private double _maxAcceleration = 180.0; // deg/s^2 or mm/s^2
    [ObservableProperty] private double _timeStep = 0.02; // 50 Hz default (0.02 s)
    [ObservableProperty] private bool _isLooping = false;
    [ObservableProperty] private bool _elbowUp = false;

    // Start Joint Angles
    [ObservableProperty] private double _startQ1 = 0.0;
    [ObservableProperty] private double _startQ2 = 45.0;
    [ObservableProperty] private double _startQ3 = -45.0;
    [ObservableProperty] private double _startQ4 = 0.0;

    // Target Joint Angles
    [ObservableProperty] private double _endQ1 = 90.0;
    [ObservableProperty] private double _endQ2 = 20.0;
    [ObservableProperty] private double _endQ3 = -20.0;
    [ObservableProperty] private double _endQ4 = 45.0;

    // Start Cartesian Pos
    [ObservableProperty] private double _startX = 250.0;
    [ObservableProperty] private double _startY = 0.0;
    [ObservableProperty] private double _startZ = 200.0;
    [ObservableProperty] private double _startPitch = 0.0;

    // Target Cartesian Pos
    [ObservableProperty] private double _endX = 150.0;
    [ObservableProperty] private double _endY = 200.0;
    [ObservableProperty] private double _endZ = 100.0;
    [ObservableProperty] private double _endPitch = -30.0;
}
