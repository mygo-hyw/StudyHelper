using System;
using System.IO;
using System.Text.Json;

namespace StudyHelper.Services
{
    public sealed class AppSettingsStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public string SettingsDirectory { get; }
        public string SettingsFilePath { get; }

        public AppSettingsStorage()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            SettingsDirectory = Path.Combine(appDataPath, "StudyHelper");
            SettingsFilePath = Path.Combine(SettingsDirectory, "ui-settings.json");
        }

        public AppSettingsSnapshot Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return AppSettingsSnapshot.CreateDefault();
                }

                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettingsSnapshot>(json, JsonOptions) ?? AppSettingsSnapshot.CreateDefault();
            }
            catch
            {
                return AppSettingsSnapshot.CreateDefault();
            }
        }

        public void Save(AppSettingsSnapshot snapshot)
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
    }

    public sealed class AppSettingsSnapshot
    {
        public string SelectedTheme { get; set; } = "经典浅色";
        public double WindowOpacity { get; set; } = 0.90;
        public bool IsDesktopEmbedded { get; set; } = true;
        public double WindowLeft { get; set; } = 50;
        public double WindowTop { get; set; } = 50;
        public double MainWindowWidth { get; set; } = 380;

        public static AppSettingsSnapshot CreateDefault() => new();
    }
}