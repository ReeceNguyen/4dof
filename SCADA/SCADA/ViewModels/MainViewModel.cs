using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCADA.Models;
using SCADA.Services;
using SCADA.Services.Interfaces;

namespace SCADA.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // Core Models
    [ObservableProperty] private RobotParameters _robot;
    [ObservableProperty] private ConnectionConfig _connectionConfig;
    [ObservableProperty] private TrajectoryConfig _trajectoryConfig;

    // Services
    public IKinematicsService KinematicsService { get; }
    public ITrajectoryService TrajectoryService { get; }
    public IModbusService ModbusService { get; }
    public IDatabaseService DatabaseService { get; }

    // Child ViewModels
    public ConnectionViewModel ConnectionVM { get; }
    public KinematicsViewModel KinematicsVM { get; }
    public TrajectoryViewModel TrajectoryVM { get; }
    public DatabaseViewModel DatabaseVM { get; }

    // Global Top Bar status
    [ObservableProperty] private int _selectedTabIndex = 1; // Default to Control tab
    [ObservableProperty] private string _plcStatusSummary = "PLC: STANDBY";
    [ObservableProperty] private string _plcStatusColor = "#10B981"; // Green
    [ObservableProperty] private bool _isPlcMoving = false;
    [ObservableProperty] private bool _isPlcInError = false;
    [ObservableProperty] private bool _isPlcInPosition = true;

    public MainViewModel()
    {
        _robot = new RobotParameters();
        _connectionConfig = new ConnectionConfig();
        _trajectoryConfig = new TrajectoryConfig();

        KinematicsService = new KinematicsService();
        TrajectoryService = new TrajectoryService(KinematicsService);
        ModbusService = new ModbusTcpService();
        DatabaseService = new DatabaseService();

        ConnectionVM = new ConnectionViewModel(ModbusService, _connectionConfig);
        KinematicsVM = new KinematicsViewModel(KinematicsService, ModbusService, _robot);
        TrajectoryVM = new TrajectoryViewModel(TrajectoryService, KinematicsService, ModbusService, _robot);
        DatabaseVM = new DatabaseViewModel(DatabaseService, _robot, _trajectoryConfig);

        DatabaseVM.OnLoadRecipeRequested = recipe =>
        {
            TrajectoryVM.GenerateTrajectory();
            SelectedTabIndex = 2; // Switch to Trajectory Tab
        };

        ModbusService.RegistersUpdated += OnModbusRegistersUpdated;
        ModbusService.StateChanged += OnModbusStateChanged;
    }

    private void OnModbusStateChanged(ConnectionState state)
    {
        string eventType = state == ConnectionState.Connected ? "Connected" :
                           state == ConnectionState.WatchdogTimeout ? "WatchdogTimeout" : "Connection";
        _ = DatabaseService.LogAlarmAsync(eventType, "ModbusTCP", $"Connection state changed to {state}");
    }

    [RelayCommand]
    public async Task EmergencyStopAsync()
    {
        TrajectoryVM.Stop();
        await ModbusService.SendEmergencyStopAsync();
        await DatabaseService.LogAlarmAsync("EStop", "Operator", "EMERGENCY STOP TRIGGERED BY OPERATOR");
    }

    [RelayCommand]
    public async Task ResetErrorAsync()
    {
        await ModbusService.SendResetErrorAsync();
    }

    [RelayCommand]
    public async Task HomeRobotAsync()
    {
        Robot.TargetQ1 = 0.0;
        Robot.TargetQ2 = 45.0;
        Robot.TargetQ3 = -45.0;
        Robot.TargetQ4 = 0.0;

        KinematicsVM.RecalculateForwardKinematics();
        KinematicsVM.RecalculateDhTable();
        KinematicsVM.UpdateJacobianAndDynamics();

        if (ModbusService.IsConnected)
        {
            await ModbusService.SendJointTargetsAsync(0, 45, -45, 0);
        }
    }

    private void OnModbusRegistersUpdated(ushort[] regs)
    {
        if (regs.Length < 3) return;

        Dispatcher.UIThread.Post(() =>
        {
            ushort statusWord = regs[2];
            bool isReady = (statusWord & 0x0001) != 0;
            IsPlcMoving = (statusWord & 0x0002) != 0;
            IsPlcInPosition = (statusWord & 0x0004) != 0;
            IsPlcInError = (statusWord & 0x0008) != 0;
            bool isHomed = (statusWord & 0x0010) != 0;
            bool isWdOk = (statusWord & 0x0020) != 0;

            if (IsPlcInError)
            {
                PlcStatusSummary = "PLC: ERROR / TRIP";
                PlcStatusColor = "#EF4444";
            }
            else if (IsPlcMoving)
            {
                PlcStatusSummary = "PLC: MOVING";
                PlcStatusColor = "#F59E0B";
            }
            else if (isReady)
            {
                PlcStatusSummary = "PLC: READY & HOMED";
                PlcStatusColor = "#10B981";
            }
            else
            {
                PlcStatusSummary = "PLC: IDLE";
                PlcStatusColor = "#94A3B8";
            }
        });
    }
}
