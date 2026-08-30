using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobusTCP.Models;
using MobusTCP.Services.Interfaces;
using MobusTCP.Views.Controls;

namespace MobusTCP.ViewModels;

public partial class KinematicsViewModel : ViewModelBase
{
    private readonly IKinematicsService _kinematicsService;
    private readonly IModbusService _modbusService;

    [ObservableProperty] private RobotParameters _robot;
    [ObservableProperty] private bool _elbowUp = false;
    [ObservableProperty] private bool _autoSyncWithModbus = true;

    // Jog Step sizes
    [ObservableProperty] private double _jointJogStep = 5.0; // deg
    [ObservableProperty] private double _cartesianJogStep = 10.0; // mm

    // Visualizer settings
    [ObservableProperty] private VisualizerViewMode _viewMode = VisualizerViewMode.Isometric3D;
    [ObservableProperty] private bool _showGhostArm = true;
    [ObservableProperty] private bool _showTrail = true;
    [ObservableProperty] private bool _showWorkspaceBoundary = true;

    // IK feedback
    [ObservableProperty] private string _ikStatusMessage = "Ready for IK Calculation.";
    [ObservableProperty] private bool _isIkValid = true;
    [ObservableProperty] private string _ikStatusColor = "#10B981";

    // Jacobian & Dynamics readouts
    [ObservableProperty] private string _jacobianFormatted = string.Empty;
    [ObservableProperty] private double _manipulability = 0.0;
    [ObservableProperty] private double _determinant = 0.0;
    [ObservableProperty] private bool _isSingular = false;
    [ObservableProperty] private string _singularityWarning = "Normal Kinematics State";

    public ObservableCollection<DhParameterRow> DhRows { get; } = new();

    public KinematicsViewModel(IKinematicsService kinematicsService, IModbusService modbusService, RobotParameters robot)
    {
        _kinematicsService = kinematicsService;
        _modbusService = modbusService;
        _robot = robot;

        RecalculateForwardKinematics();
        RecalculateDhTable();
        UpdateJacobianAndDynamics();

        _modbusService.RegistersUpdated += OnModbusRegistersUpdated;
    }

    public void RecalculateForwardKinematics()
    {
        var fk = _kinematicsService.ForwardKinematics(Robot, Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4);
        Robot.X = fk.X;
        Robot.Y = fk.Y;
        Robot.Z = fk.Z;
        Robot.Pitch = fk.Pitch;
        Robot.Yaw = fk.Yaw;
        Robot.Roll = fk.Roll;

        Robot.BasePos = fk.P0;
        Robot.Joint1Pos = fk.P1;
        Robot.Joint2Pos = fk.P2;
        Robot.Joint3Pos = fk.P3;
        Robot.EndEffectorPos = fk.P4;

        // Also calculate ghost target arm positions
        var fkTarget = _kinematicsService.ForwardKinematics(Robot, Robot.TargetQ1, Robot.TargetQ2, Robot.TargetQ3, Robot.TargetQ4);
        Robot.TargetJoint1Pos = fkTarget.P1;
        Robot.TargetJoint2Pos = fkTarget.P2;
        Robot.TargetJoint3Pos = fkTarget.P3;
        Robot.TargetEndEffectorPos = fkTarget.P4;
    }

    public void RecalculateDhTable()
    {
        var rows = _kinematicsService.CalculateDhTable(Robot, Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4);
        DhRows.Clear();
        foreach (var r in rows)
        {
            DhRows.Add(r);
        }
    }

    public void UpdateJacobianAndDynamics()
    {
        var jac = _kinematicsService.CalculateJacobian(Robot, Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4);
        Determinant = jac.Determinant;
        Manipulability = jac.Manipulability;
        IsSingular = jac.IsSingular;
        SingularityWarning = IsSingular
            ? "⚠️ SINGULARITY DETECTED! Arm near workspace boundary or aligned joint."
            : "✓ Normal Kinematics State";

        JacobianFormatted = $"[{jac.J[0, 0]:F1}, {jac.J[0, 1]:F1}, {jac.J[0, 2]:F1}, {jac.J[0, 3]:F1}]\n" +
                            $"[{jac.J[1, 0]:F1}, {jac.J[1, 1]:F1}, {jac.J[1, 2]:F1}, {jac.J[1, 3]:F1}]\n" +
                            $"[{jac.J[2, 0]:F1}, {jac.J[2, 1]:F1}, {jac.J[2, 2]:F1}, {jac.J[2, 3]:F1}]\n" +
                            $"[{jac.J[3, 0]:F1}, {jac.J[3, 1]:F1}, {jac.J[3, 2]:F1}, {jac.J[3, 3]:F1}]";

        var dyn = _kinematicsService.CalculateDynamics(
            Robot,
            Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4,
            Robot.Q1Dot, Robot.Q2Dot, Robot.Q3Dot, Robot.Q4Dot,
            Robot.Q1Ddot, Robot.Q2Ddot, Robot.Q3Ddot, Robot.Q4Ddot);

        Robot.Tau1 = dyn.Tau1;
        Robot.Tau2 = dyn.Tau2;
        Robot.Tau3 = dyn.Tau3;
        Robot.Tau4 = dyn.Tau4;
        Robot.TotalPowerWatts = dyn.TotalPower;
    }

    [RelayCommand]
    public void CalculateFk()
    {
        RecalculateForwardKinematics();
        RecalculateDhTable();
        UpdateJacobianAndDynamics();
    }

    [RelayCommand]
    public async Task SendFkToRobotAsync()
    {
        Robot.CopyJointsToTarget();
        RecalculateForwardKinematics();
        if (_modbusService.IsConnected)
        {
            await _modbusService.SendJointTargetsAsync(Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4);
        }
    }

    [RelayCommand]
    public void CalculateIk()
    {
        var ik = _kinematicsService.InverseKinematics(Robot, Robot.TargetX, Robot.TargetY, Robot.TargetZ, Robot.TargetPitch, ElbowUp);
        IsIkValid = ik.IsReachable;
        IkStatusMessage = ik.Message;
        IkStatusColor = IsIkValid ? "#10B981" : "#EF4444";

        if (ik.IsReachable)
        {
            Robot.TargetQ1 = ik.Q1;
            Robot.TargetQ2 = ik.Q2;
            Robot.TargetQ3 = ik.Q3;
            Robot.TargetQ4 = ik.Q4;

            var fkTarget = _kinematicsService.ForwardKinematics(Robot, Robot.TargetQ1, Robot.TargetQ2, Robot.TargetQ3, Robot.TargetQ4);
            Robot.TargetJoint1Pos = fkTarget.P1;
            Robot.TargetJoint2Pos = fkTarget.P2;
            Robot.TargetJoint3Pos = fkTarget.P3;
            Robot.TargetEndEffectorPos = fkTarget.P4;
        }
    }

    [RelayCommand]
    public async Task ApplyIkToRobotAsync()
    {
        CalculateIk();
        if (IsIkValid)
        {
            Robot.Q1 = Robot.TargetQ1;
            Robot.Q2 = Robot.TargetQ2;
            Robot.Q3 = Robot.TargetQ3;
            Robot.Q4 = Robot.TargetQ4;

            RecalculateForwardKinematics();
            RecalculateDhTable();
            UpdateJacobianAndDynamics();

            if (_modbusService.IsConnected)
            {
                await _modbusService.SendJointTargetsAsync(Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4);
                await _modbusService.SendCartesianTargetsAsync(Robot.TargetX, Robot.TargetY, Robot.TargetZ, Robot.TargetPitch);
            }
        }
    }

    [RelayCommand]
    public async Task JogJointAsync(string jointAndDirection)
    {
        // jointAndDirection format: "1+", "1-", "2+", "2-", etc.
        if (string.IsNullOrEmpty(jointAndDirection) || jointAndDirection.Length < 2) return;

        char joint = jointAndDirection[0];
        char dir = jointAndDirection[1];
        double delta = (dir == '+' ? 1.0 : -1.0) * JointJogStep;

        switch (joint)
        {
            case '1': Robot.Q1 = Math.Clamp(Robot.Q1 + delta, Robot.Q1Min, Robot.Q1Max); break;
            case '2': Robot.Q2 = Math.Clamp(Robot.Q2 + delta, Robot.Q2Min, Robot.Q2Max); break;
            case '3': Robot.Q3 = Math.Clamp(Robot.Q3 + delta, Robot.Q3Min, Robot.Q3Max); break;
            case '4': Robot.Q4 = Math.Clamp(Robot.Q4 + delta, Robot.Q4Min, Robot.Q4Max); break;
        }

        Robot.CopyJointsToTarget();
        RecalculateForwardKinematics();
        RecalculateDhTable();
        UpdateJacobianAndDynamics();

        if (AutoSyncWithModbus && _modbusService.IsConnected)
        {
            await _modbusService.SendJointTargetsAsync(Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4);
        }
    }

    [RelayCommand]
    public async Task JogCartesianAsync(string axisAndDirection)
    {
        // axisAndDirection format: "X+", "X-", "Y+", "Y-", "Z+", "Z-", "P+", "P-"
        if (string.IsNullOrEmpty(axisAndDirection) || axisAndDirection.Length < 2) return;

        char axis = axisAndDirection[0];
        char dir = axisAndDirection[1];
        double delta = (dir == '+' ? 1.0 : -1.0) * CartesianJogStep;

        double targetX = Robot.X;
        double targetY = Robot.Y;
        double targetZ = Robot.Z;
        double targetPitch = Robot.Pitch;

        switch (axis)
        {
            case 'X': targetX += delta; break;
            case 'Y': targetY += delta; break;
            case 'Z': targetZ += delta; break;
            case 'P': targetPitch += (dir == '+' ? 1.0 : -1.0) * 5.0; break;
        }

        var ik = _kinematicsService.InverseKinematics(Robot, targetX, targetY, targetZ, targetPitch, ElbowUp);
        if (ik.IsReachable)
        {
            Robot.TargetX = targetX;
            Robot.TargetY = targetY;
            Robot.TargetZ = targetZ;
            Robot.TargetPitch = targetPitch;

            Robot.Q1 = ik.Q1;
            Robot.Q2 = ik.Q2;
            Robot.Q3 = ik.Q3;
            Robot.Q4 = ik.Q4;

            Robot.CopyJointsToTarget();
            RecalculateForwardKinematics();
            RecalculateDhTable();
            UpdateJacobianAndDynamics();

            if (AutoSyncWithModbus && _modbusService.IsConnected)
            {
                await _modbusService.SendJointTargetsAsync(Robot.Q1, Robot.Q2, Robot.Q3, Robot.Q4);
            }
        }
        else
        {
            IkStatusMessage = ik.Message;
        }
    }

    [RelayCommand]
    public async Task ToggleGripperAsync()
    {
        Robot.IsGripperClosed = !Robot.IsGripperClosed;
        if (_modbusService.IsConnected)
        {
            await _modbusService.SendGripperCommandAsync(Robot.IsGripperClosed);
        }
    }

    private void OnModbusRegistersUpdated(ushort[] regs)
    {
        if (!AutoSyncWithModbus || regs.Length < 19) return;

        Dispatcher.UIThread.Post(() =>
        {
            // If in live sync mode and not currently dragging sliders, update actual position feedback
            // Actual angles at offset 11, 13, 15, 17
            float actQ1 = ReadFloat(regs, 11);
            float actQ2 = ReadFloat(regs, 13);
            float actQ3 = ReadFloat(regs, 15);
            float actQ4 = ReadFloat(regs, 17);

            // Update parameters
            if (Math.Abs(actQ1 - Robot.Q1) > 0.05 || Math.Abs(actQ2 - Robot.Q2) > 0.05 ||
                Math.Abs(actQ3 - Robot.Q3) > 0.05 || Math.Abs(actQ4 - Robot.Q4) > 0.05)
            {
                Robot.Q1 = actQ1;
                Robot.Q2 = actQ2;
                Robot.Q3 = actQ3;
                Robot.Q4 = actQ4;

                RecalculateForwardKinematics();
                RecalculateDhTable();
                UpdateJacobianAndDynamics();
            }
        });
    }

    [RelayCommand]
    public void SetViewMode(string mode)
    {
        ViewMode = mode switch
        {
            "Side" => VisualizerViewMode.SideElevation2D,
            "Top" => VisualizerViewMode.TopDown2D,
            _ => VisualizerViewMode.Isometric3D
        };
    }

    private static float ReadFloat(ushort[] regs, int offset)
    {
        if (offset + 1 >= regs.Length) return 0f;
        ushort hw = regs[offset];
        ushort lw = regs[offset + 1];
        byte[] bytes = [(byte)(lw & 0xFF), (byte)(lw >> 8), (byte)(hw & 0xFF), (byte)(hw >> 8)];
        return BitConverter.ToSingle(bytes, 0);
    }
}
