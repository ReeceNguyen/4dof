using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OptaLedController.Models;
namespace OptaLedController.Services;

public class JsonDataLogger
{
    private readonly string _filePath;

    public JsonDataLogger(string fileName = "app_state.json")
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
    }

    public async Task SaveStateAsync(bool isLedOn)
    {
        try
        {
            var data = new AppStateData
            {
                IsLedOn = isLedOn,
                LastUpdated = DateTime.Now
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(_filePath, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logger Error] Không thể lưu JSON: {ex.Message}");
        }
    }

    public async Task<AppStateData?> LoadStateAsync()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;

            string jsonString = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<AppStateData>(jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logger Error] Không thể đọc JSON: {ex.Message}");
            return null;
        }
    }
}