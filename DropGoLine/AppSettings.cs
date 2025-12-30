using System;
using System.IO;
using System.Text.Json;

namespace DropGoLine {
  public class AppSettings {
    private static AppSettings? _current;
    
    public static AppSettings Current {
      get {
        if (_current == null) {
          _current = Load();
        }
        return _current;
      }
    }

    public string ServerIP { get; set; } = "127.0.0.1";
    
    public string DeviceName { get; set; } = string.Empty;

    private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public static AppSettings Load() {
      AppSettings settings = new AppSettings();
      if (File.Exists(ConfigPath)) {
        try {
          string json = File.ReadAllText(ConfigPath);
          settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        } catch { }
      }

      if (string.IsNullOrEmpty(settings.DeviceName)) {
        var rnd = new Random();
        settings.DeviceName = $"Device-{rnd.Next(1000, 9999)}";
        settings.Save();
      }
      return settings;
    }

    public void Save() {
      try {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(ConfigPath, json);
      } catch {
        // Handle save error if needed
      }
    }
  }
}
