using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobusTCP.Models;
using MobusTCP.Services.Interfaces;

namespace MobusTCP.ViewModels;

public partial class DatabaseViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly RobotParameters _robot;
    private readonly TrajectoryConfig _trajectoryConfig;

    // Sub-tab selection (0 = Telemetry Historian, 1 = Alarms, 2 = Recipes)
    [ObservableProperty] private int _selectedSubTabIndex = 0;

    // 1. Telemetry Historian
    public ObservableCollection<TelemetryLogEntry> TelemetryList { get; } = new();
    [ObservableProperty] private long _totalRecordsCount = 0;
    [ObservableProperty] private int _queryLimit = 200;
    [ObservableProperty] private string _telemetryFilterPeriod = "All"; // Last 5m, Last 1h, Today, All
    [ObservableProperty] private bool _autoLoggingEnabled = true;
    [ObservableProperty] private int _autoLoggingIntervalMs = 500;
    [ObservableProperty] private string _dbStatusMessage = "Database ready.";

    // 2. Alarms & Events
    public ObservableCollection<AlarmEventEntry> AlarmList { get; } = new();
    [ObservableProperty] private AlarmEventEntry? _selectedAlarm;

    // 3. Recipes & Teach Points
    public ObservableCollection<RecipeProgram> RecipeList { get; } = new();
    [ObservableProperty] private RecipeProgram? _selectedRecipe;
    [ObservableProperty] private string _newRecipeName = "New Recipe";
    [ObservableProperty] private string _newRecipeDescription = "Description";

    private readonly DispatcherTimer _autoLogTimer;

    public Action<RecipeProgram>? OnLoadRecipeRequested;

    public DatabaseViewModel(IDatabaseService databaseService, RobotParameters robot, TrajectoryConfig trajectoryConfig)
    {
        _databaseService = databaseService;
        _robot = robot;
        _trajectoryConfig = trajectoryConfig;

        _autoLogTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_autoLoggingIntervalMs)
        };
        _autoLogTimer.Tick += OnAutoLogTimerTick;
        if (_autoLoggingEnabled) _autoLogTimer.Start();

        _ = InitializeAndLoadAsync();
    }

    private async Task InitializeAndLoadAsync()
    {
        await _databaseService.InitializeDatabaseAsync();
        await RefreshTelemetryAsync();
        await RefreshAlarmsAsync();
        await RefreshRecipesAsync();
    }

    private async void OnAutoLogTimerTick(object? sender, EventArgs e)
    {
        if (!AutoLoggingEnabled) return;

        try
        {
            var entry = new TelemetryLogEntry
            {
                Timestamp = DateTime.Now,
                Q1 = _robot.Q1,
                Q2 = _robot.Q2,
                Q3 = _robot.Q3,
                Q4 = _robot.Q4,
                X = _robot.X,
                Y = _robot.Y,
                Z = _robot.Z,
                Pitch = _robot.Pitch,
                Tau1 = _robot.Tau1,
                Tau2 = _robot.Tau2,
                Tau3 = _robot.Tau3,
                Tau4 = _robot.Tau4,
                TotalPower = _robot.TotalPowerWatts,
                PlcStatus = _robot.IsSingular ? "SINGULAR" : "NORMAL",
                LatencyMs = 0.0
            };

            await _databaseService.LogTelemetryAsync(entry);
        }
        catch
        {
            // Ignore auto-log background write errors
        }
    }

    partial void OnAutoLoggingEnabledChanged(bool value)
    {
        if (value) _autoLogTimer.Start();
        else _autoLogTimer.Stop();
    }

    partial void OnAutoLoggingIntervalMsChanged(int value)
    {
        _autoLogTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, value));
    }

    [RelayCommand]
    public async Task RefreshTelemetryAsync()
    {
        DateTime? start = TelemetryFilterPeriod switch
        {
            "Last 5m" => DateTime.UtcNow.AddMinutes(-5),
            "Last 1h" => DateTime.UtcNow.AddHours(-1),
            "Today" => DateTime.UtcNow.Date,
            _ => null
        };

        var logs = await _databaseService.GetTelemetryHistoryAsync(start, null, QueryLimit);
        TotalRecordsCount = await _databaseService.GetTelemetryCountAsync();

        TelemetryList.Clear();
        foreach (var item in logs)
        {
            TelemetryList.Add(item);
        }

        DbStatusMessage = $"Loaded {TelemetryList.Count} historian records (Total in DB: {TotalRecordsCount}).";
    }

    public string HistorianDirectory => _databaseService.HistorianDirectory;

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        try
        {
            string exportedPath = await _databaseService.ExportTelemetryToCsvAsync();
            DbStatusMessage = $"Exported CSV to Historian: {Path.GetFileName(exportedPath)}";
        }
        catch (Exception ex)
        {
            DbStatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenHistorianFolder()
    {
        try
        {
            string dir = _databaseService.HistorianDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            DbStatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PurgeOldLogsAsync()
    {
        int deleted = await _databaseService.ClearTelemetryOlderThanAsync(7);
        await RefreshTelemetryAsync();
        DbStatusMessage = $"Purged {deleted} records older than 7 days.";
    }

    [RelayCommand]
    public async Task RefreshAlarmsAsync()
    {
        var alarms = await _databaseService.GetAlarmsHistoryAsync(100);
        AlarmList.Clear();
        foreach (var a in alarms)
        {
            AlarmList.Add(a);
        }
    }

    [RelayCommand]
    public async Task AcknowledgeAlarmAsync(AlarmEventEntry? alarm)
    {
        if (alarm == null) return;
        await _databaseService.AcknowledgeAlarmAsync(alarm.Id);
        alarm.Acknowledged = true;
    }

    [RelayCommand]
    public async Task RefreshRecipesAsync()
    {
        var recipes = await _databaseService.GetRecipesAsync();
        RecipeList.Clear();
        foreach (var r in recipes)
        {
            RecipeList.Add(r);
        }
        if (RecipeList.Count > 0 && SelectedRecipe == null)
        {
            SelectedRecipe = RecipeList[0];
        }
    }

    [RelayCommand]
    public async Task SaveCurrentTrajectoryAsRecipeAsync()
    {
        var recipe = new RecipeProgram
        {
            Name = string.IsNullOrWhiteSpace(NewRecipeName) ? $"Recipe {DateTime.Now:HH:mm:ss}" : NewRecipeName,
            Description = NewRecipeDescription,
            CreatedAt = DateTime.Now,
            ProfileType = _trajectoryConfig.ProfileType.ToString(),
            Duration = _trajectoryConfig.Duration,
            StartQ1 = _trajectoryConfig.StartQ1,
            StartQ2 = _trajectoryConfig.StartQ2,
            StartQ3 = _trajectoryConfig.StartQ3,
            StartQ4 = _trajectoryConfig.StartQ4,
            EndQ1 = _trajectoryConfig.EndQ1,
            EndQ2 = _trajectoryConfig.EndQ2,
            EndQ3 = _trajectoryConfig.EndQ3,
            EndQ4 = _trajectoryConfig.EndQ4,
            StartX = _trajectoryConfig.StartX,
            StartY = _trajectoryConfig.StartY,
            StartZ = _trajectoryConfig.StartZ,
            StartPitch = _trajectoryConfig.StartPitch,
            EndX = _trajectoryConfig.EndX,
            EndY = _trajectoryConfig.EndY,
            EndZ = _trajectoryConfig.EndZ,
            EndPitch = _trajectoryConfig.EndPitch
        };

        long id = await _databaseService.SaveRecipeAsync(recipe);
        recipe.Id = id;
        await RefreshRecipesAsync();
        DbStatusMessage = $"Saved recipe '{recipe.Name}' (ID: {id}).";
    }

    [RelayCommand]
    public void LoadSelectedRecipeToPlanner()
    {
        if (SelectedRecipe == null) return;

        _trajectoryConfig.Duration = SelectedRecipe.Duration;
        _trajectoryConfig.StartQ1 = SelectedRecipe.StartQ1;
        _trajectoryConfig.StartQ2 = SelectedRecipe.StartQ2;
        _trajectoryConfig.StartQ3 = SelectedRecipe.StartQ3;
        _trajectoryConfig.StartQ4 = SelectedRecipe.StartQ4;

        _trajectoryConfig.EndQ1 = SelectedRecipe.EndQ1;
        _trajectoryConfig.EndQ2 = SelectedRecipe.EndQ2;
        _trajectoryConfig.EndQ3 = SelectedRecipe.EndQ3;
        _trajectoryConfig.EndQ4 = SelectedRecipe.EndQ4;

        _trajectoryConfig.StartX = SelectedRecipe.StartX;
        _trajectoryConfig.StartY = SelectedRecipe.StartY;
        _trajectoryConfig.StartZ = SelectedRecipe.StartZ;
        _trajectoryConfig.StartPitch = SelectedRecipe.StartPitch;

        _trajectoryConfig.EndX = SelectedRecipe.EndX;
        _trajectoryConfig.EndY = SelectedRecipe.EndY;
        _trajectoryConfig.EndZ = SelectedRecipe.EndZ;
        _trajectoryConfig.EndPitch = SelectedRecipe.EndPitch;

        OnLoadRecipeRequested?.Invoke(SelectedRecipe);
        DbStatusMessage = $"Loaded recipe '{SelectedRecipe.Name}' into Trajectory Planner.";
    }

    [RelayCommand]
    public async Task DeleteSelectedRecipeAsync()
    {
        if (SelectedRecipe == null) return;
        await _databaseService.DeleteRecipeAsync(SelectedRecipe.Id);
        await RefreshRecipesAsync();
        DbStatusMessage = "Deleted recipe.";
    }
}
