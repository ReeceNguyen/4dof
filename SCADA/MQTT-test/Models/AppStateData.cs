using System;

namespace OptaLedController.Models;

public class AppStateData
{
    public bool IsLedOn { get; set; } = false;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}