using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MobusTCP.Models;

public partial class TelemetryLogEntry : ObservableObject
{
    [ObservableProperty] private long _id;
    [ObservableProperty] private DateTime _timestamp = DateTime.Now;
    [ObservableProperty] private double _q1;
    [ObservableProperty] private double _q2;
    [ObservableProperty] private double _q3;
    [ObservableProperty] private double _q4;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _z;
    [ObservableProperty] private double _pitch;
    [ObservableProperty] private double _tau1;
    [ObservableProperty] private double _tau2;
    [ObservableProperty] private double _tau3;
    [ObservableProperty] private double _tau4;
    [ObservableProperty] private double _totalPower;
    [ObservableProperty] private string _plcStatus = "READY";
    [ObservableProperty] private double _latencyMs;

    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
}

public partial class AlarmEventEntry : ObservableObject
{
    [ObservableProperty] private long _id;
    [ObservableProperty] private DateTime _timestamp = DateTime.Now;
    [ObservableProperty] private string _eventType = "Info"; // Alarm, Warning, Info, EStop, WatchdogTimeout
    [ObservableProperty] private string _source = "System";
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _acknowledged = false;

    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    public string ColorHex => EventType switch
    {
        "Alarm" or "EStop" or "WatchdogTimeout" => "#EF4444", // Red
        "Warning" => "#F59E0B",                              // Amber
        "Success" or "Connected" => "#10B981",               // Green
        _ => "#38BDF8"                                       // Blue/Info
    };
}

public partial class RecipeProgram : ObservableObject
{
    [ObservableProperty] private long _id;
    [ObservableProperty] private string _name = "Program 1";
    [ObservableProperty] private string _description = "4DOF motion recipe";
    [ObservableProperty] private DateTime _createdAt = DateTime.Now;
    [ObservableProperty] private string _profileType = "QuinticPolynomial";
    [ObservableProperty] private double _duration = 3.0;

    [ObservableProperty] private double _startQ1;
    [ObservableProperty] private double _startQ2 = 45.0;
    [ObservableProperty] private double _startQ3 = -45.0;
    [ObservableProperty] private double _startQ4;

    [ObservableProperty] private double _endQ1 = 90.0;
    [ObservableProperty] private double _endQ2 = 10.0;
    [ObservableProperty] private double _endQ3 = -30.0;
    [ObservableProperty] private double _endQ4 = 20.0;

    [ObservableProperty] private double _startX = 250.0;
    [ObservableProperty] private double _startY;
    [ObservableProperty] private double _startZ = 200.0;
    [ObservableProperty] private double _startPitch;

    [ObservableProperty] private double _endX = 150.0;
    [ObservableProperty] private double _endY = 200.0;
    [ObservableProperty] private double _endZ = 100.0;
    [ObservableProperty] private double _endPitch = -30.0;

    public string FormattedCreatedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm");
}
