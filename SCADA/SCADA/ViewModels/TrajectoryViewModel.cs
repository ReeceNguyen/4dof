using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCADA.Models;
using SCADA.Services.Interfaces;
using SCADA.Views.Controls;

namespace SCADA.ViewModels;

public partial class TrajectoryViewModel : ViewModelBase
{
    private readonly ITrajectoryService _trajectoryService;
    private readonly IKinematicsService _kinematicsService;
    private readonly IModbusService _modbusService;
    private readonly RobotParameters _robot;

    [ObservableProperty] private TrajectoryConfig _config = new();
    [ObservableProperty] private List<TrajectoryPoint> _trajectoryPoints = new();
    [ObservableProperty] private double _currentTime = 0.0;
    [ObservableProperty] private bool _isPlaying = false;
    [ObservableProperty] private bool _isPaused = false;
    [ObservableProperty] private ChartDataType _selectedChartType = ChartDataType.JointPositions;
    [ObservableProperty] private string _chartTitle = "Joint Positions (deg)";
    [ObservableProperty] private string _trajectoryStatusMessage = "No trajectory generated.";
    [ObservableProperty] private int _selectedProfileIndex = 0;

    public ObservableCollection<string> ProfileTypes { get; } =
    [
        "Bậc 5 (Quintic Polynomial - Smooth)",
        "Bậc 3 (Cubic Polynomial)",
        "Hình thang (Trapezoidal LSPB)",
        "Đường thẳng Đề-các (Linear Cartesian)"
    ];

    partial void OnSelectedProfileIndexChanged(int value)
    {
        Config.ProfileType = value switch
        {
            1 => TrajectoryProfileType.CubicPolynomial,
            2 => TrajectoryProfileType.TrapezoidalVelocity,
            3 => TrajectoryProfileType.LinearCartesian,
            _ => TrajectoryProfileType.QuinticPolynomial
        };
        GenerateTrajectory();
    }

    private readonly DispatcherTimer _playbackTimer;
    private DateTime _playbackStartTime;
    private double _pausedTimeOffset = 0.0;

    public Action<double, double, double>? OnNewTrailPoint;
    public Action? OnClearTrail;

    public TrajectoryViewModel(
        ITrajectoryService trajectoryService,
        IKinematicsService kinematicsService,
        IModbusService modbusService,
        RobotParameters robot)
    {
        _trajectoryService = trajectoryService;
        _kinematicsService = kinematicsService;
        _modbusService = modbusService;
        _robot = robot;

        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(20) // 50 Hz execution loop
        };
        _playbackTimer.Tick += OnPlaybackTimerTick;

        GenerateTrajectory();
    }

    [RelayCommand]
    public void GenerateTrajectory()
    {
        TrajectoryPoints = _trajectoryService.GenerateTrajectory(_robot, Config);
        CurrentTime = 0.0;
        _pausedTimeOffset = 0.0;
        TrajectoryStatusMessage = $"Generated {TrajectoryPoints.Count} points over {Config.Duration:F1}s ({Config.ProfileType})";
    }

    [RelayCommand]
    public void Play()
    {
        if (TrajectoryPoints.Count == 0)
        {
            GenerateTrajectory();
        }

        if (IsPlaying && !IsPaused) return;

        if (IsPaused)
        {
            _playbackStartTime = DateTime.UtcNow - TimeSpan.FromSeconds(_pausedTimeOffset);
            IsPaused = false;
        }
        else
        {
            _playbackStartTime = DateTime.UtcNow;
            _pausedTimeOffset = 0.0;
            CurrentTime = 0.0;
            OnClearTrail?.Invoke();
        }

        IsPlaying = true;
        _playbackTimer.Start();
    }

    [RelayCommand]
    public void Pause()
    {
        if (!IsPlaying) return;
        _playbackTimer.Stop();
        _pausedTimeOffset = CurrentTime;
        IsPaused = true;
        IsPlaying = false;
    }

    [RelayCommand]
    public void Stop()
    {
        _playbackTimer.Stop();
        IsPlaying = false;
        IsPaused = false;
        CurrentTime = 0.0;
        _pausedTimeOffset = 0.0;

        if (TrajectoryPoints.Count > 0)
        {
            ApplyPointToRobot(TrajectoryPoints[0]);
        }
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs e)
    {
        if (TrajectoryPoints.Count == 0)
        {
            Stop();
            return;
        }

        double elapsed = (DateTime.UtcNow - _playbackStartTime).TotalSeconds;
        double maxTime = Config.Duration;

        if (elapsed >= maxTime)
        {
            if (Config.IsLooping)
            {
                _playbackStartTime = DateTime.UtcNow;
                elapsed = 0.0;
            }
            else
            {
                CurrentTime = maxTime;
                ApplyPointToRobot(TrajectoryPoints[^1]);
                Stop();
                return;
            }
        }

        CurrentTime = elapsed;

        // Find interpolated point in TrajectoryPoints
        int index = (int)((elapsed / maxTime) * (TrajectoryPoints.Count - 1));
        index = Math.Clamp(index, 0, TrajectoryPoints.Count - 1);
        var pt = TrajectoryPoints[index];

        ApplyPointToRobot(pt);
    }

    private void ApplyPointToRobot(TrajectoryPoint pt)
    {
        _robot.Q1 = pt.Q1;
        _robot.Q2 = pt.Q2;
        _robot.Q3 = pt.Q3;
        _robot.Q4 = pt.Q4;

        _robot.Q1Dot = pt.Q1Dot;
        _robot.Q2Dot = pt.Q2Dot;
        _robot.Q3Dot = pt.Q3Dot;
        _robot.Q4Dot = pt.Q4Dot;

        _robot.Q1Ddot = pt.Q1Ddot;
        _robot.Q2Ddot = pt.Q2Ddot;
        _robot.Q3Ddot = pt.Q3Ddot;
        _robot.Q4Ddot = pt.Q4Ddot;

        _robot.Tau1 = pt.Tau1;
        _robot.Tau2 = pt.Tau2;
        _robot.Tau3 = pt.Tau3;
        _robot.Tau4 = pt.Tau4;

        _robot.X = pt.X;
        _robot.Y = pt.Y;
        _robot.Z = pt.Z;
        _robot.Pitch = pt.Pitch;

        // Recalculate 3D positions for visualizer
        var fk = _kinematicsService.ForwardKinematics(_robot, pt.Q1, pt.Q2, pt.Q3, pt.Q4);
        _robot.BasePos = fk.P0;
        _robot.Joint1Pos = fk.P1;
        _robot.Joint2Pos = fk.P2;
        _robot.Joint3Pos = fk.P3;
        _robot.EndEffectorPos = fk.P4;

        OnNewTrailPoint?.Invoke(pt.X, pt.Y, pt.Z);

        // Stream to Modbus PLC if connected
        if (_modbusService.IsConnected)
        {
            _ = _modbusService.SendJointTargetsAsync(pt.Q1, pt.Q2, pt.Q3, pt.Q4);
        }
    }

    [RelayCommand]
    public void SetStartFromCurrent()
    {
        Config.StartQ1 = _robot.Q1;
        Config.StartQ2 = _robot.Q2;
        Config.StartQ3 = _robot.Q3;
        Config.StartQ4 = _robot.Q4;

        Config.StartX = _robot.X;
        Config.StartY = _robot.Y;
        Config.StartZ = _robot.Z;
        Config.StartPitch = _robot.Pitch;

        GenerateTrajectory();
    }

    [RelayCommand]
    public void SetEndFromCurrent()
    {
        Config.EndQ1 = _robot.Q1;
        Config.EndQ2 = _robot.Q2;
        Config.EndQ3 = _robot.Q3;
        Config.EndQ4 = _robot.Q4;

        Config.EndX = _robot.X;
        Config.EndY = _robot.Y;
        Config.EndZ = _robot.Z;
        Config.EndPitch = _robot.Pitch;

        GenerateTrajectory();
    }

    [RelayCommand]
    public void SelectChart(string chartType)
    {
        SelectedChartType = chartType switch
        {
            "Velocities" => ChartDataType.JointVelocities,
            "Torques" => ChartDataType.JointTorques,
            "Cartesian" => ChartDataType.CartesianPositions,
            _ => ChartDataType.JointPositions
        };

        ChartTitle = chartType switch
        {
            "Velocities" => "Joint Angular Velocities (deg/s)",
            "Torques" => "Euler-Lagrange Joint Torques (N*m)",
            "Cartesian" => "Cartesian Path Position (mm)",
            _ => "Joint Positions (deg)"
        };
    }
}
