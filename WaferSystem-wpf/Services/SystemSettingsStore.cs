using Newtonsoft.Json;
using System;
using System.IO;
using WaferSystem.Wpf.Models;

namespace WaferSystem.Wpf.Services
{
    public enum SystemSettingsLoadStatus
    {
        Success,
        MissingFile,
        DeserializeFailed
    }

    public class SystemSettingsLoadResult
    {
        public SystemSettingsLoadStatus Status { get; set; }
        public SystemSettings Settings { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class SystemSettingsStore
    {
        private const string ConfigDirectoryName = "ConfigFile";
        private const string ConfigFileName = "SystemSettings.json";

        public static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigDirectoryName, ConfigFileName);

        public static SystemSettingsLoadResult Load()
        {
            if (!File.Exists(FilePath))
            {
                return new SystemSettingsLoadResult
                {
                    Status = SystemSettingsLoadStatus.MissingFile,
                    Settings = new SystemSettings()
                };
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                return new SystemSettingsLoadResult
                {
                    Status = SystemSettingsLoadStatus.Success,
                    Settings = Normalize(JsonConvert.DeserializeObject<SystemSettings>(json) ?? new SystemSettings())
                };
            }
            catch (Exception ex)
            {
                return new SystemSettingsLoadResult
                {
                    Status = SystemSettingsLoadStatus.DeserializeFailed,
                    ErrorMessage = "系统设置反序列化失败。" +
                        Environment.NewLine +
                        "文件：" + FilePath +
                        Environment.NewLine +
                        "原因：" + ex.Message
                };
            }
        }

        public static bool Save(SystemSettings settings)
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Normalize(settings), Formatting.Indented));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static SystemSettings Normalize(SystemSettings settings)
        {
            settings ??= new SystemSettings();
            settings.Motion ??= new MotionSettings();
            settings.Vision ??= new VisionSettings();
            settings.WaferMap ??= new WaferMapRuntimeSettings();
            return settings;
        }
    }
}
