using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;

namespace MyAsset.Wpf
{
    public static class JsonFileHelper
    {
        public static T DeepClone<T>(this T source) where T : class
        {
            if (source == null)
                return null;

            string json = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static void CopyProperties<T>(this T source, T target) where T : class
        {
            if (source == null || target == null)
                return;

            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                if (property.CanWrite)
                    property.SetValue(target, property.GetValue(source));
            }
        }

        public static void ApplyValuesFrom<T>(this T target, T source) where T : class
        {
            if (target == null)
            {
                MyMessageBox.ShowWarn("目标对象不能为 null", "数据警告");
                return;
            }

            if (source == null)
                return;

            string json = JsonConvert.SerializeObject(source);
            var settings = new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            JsonConvert.PopulateObject(json, target, settings);
        }

        public static bool SaveToDialog<T>(this T data, string defaultFileName = "data.json", string title = "保存 JSON 数据")
        {
            if (data == null)
            {
                MyMessageBox.ShowWarn("要保存的数据不能为空", "保存警告");
                return false;
            }

            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = "json",
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() != true)
                return false;

            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
                return true;
            }
            catch (Exception ex)
            {
                MyMessageBox.ShowError($"保存文件失败:\n{ex.Message}", "保存错误");
                return false;
            }
        }

        public static bool LoadFromDialog<T>(this T targetObject, string title = "打开 JSON 数据") where T : class
        {
            if (targetObject == null)
            {
                MyMessageBox.ShowWarn("目标注入对象不能为空", "读取警告");
                return false;
            }

            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return false;

            try
            {
                string json = File.ReadAllText(dialog.FileName);
                var settings = new JsonSerializerSettings
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                };

                JsonConvert.PopulateObject(json, targetObject, settings);
                return true;
            }
            catch (Exception ex)
            {
                MyMessageBox.ShowError($"读取或解析文件失败:\n{ex.Message}", "读取错误");
                return false;
            }
        }

        public static bool SaveToFile<T>(this T data, string filePath)
        {
            if (data == null || string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static T LoadFromFile<T>(string filePath) where T : class
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
