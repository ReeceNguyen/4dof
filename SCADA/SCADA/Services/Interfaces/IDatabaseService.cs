using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SCADA.Models;

namespace SCADA.Services.Interfaces;

public interface IDatabaseService
{
    string DatabasePath { get; }
    string HistorianDirectory { get; }

    Task InitializeDatabaseAsync();

    Task LogTelemetryAsync(TelemetryLogEntry entry);
    Task LogAlarmAsync(string eventType, string source, string message);

    Task<List<TelemetryLogEntry>> GetTelemetryHistoryAsync(DateTime? start = null, DateTime? end = null, int limit = 500);
    Task<List<AlarmEventEntry>> GetAlarmsHistoryAsync(int limit = 100);
    Task AcknowledgeAlarmAsync(long alarmId);

    Task<List<RecipeProgram>> GetRecipesAsync();
    Task<long> SaveRecipeAsync(RecipeProgram recipe);
    Task<bool> DeleteRecipeAsync(long recipeId);

    Task<int> ClearTelemetryOlderThanAsync(int days);
    Task<string> ExportTelemetryToCsvAsync(string? filePath = null, DateTime? start = null, DateTime? end = null);
    Task<long> GetTelemetryCountAsync();
}
