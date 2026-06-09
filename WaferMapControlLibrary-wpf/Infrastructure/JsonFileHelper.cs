using Newtonsoft.Json;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace WaferMap.Wpf.Infrastructure
{
    /// <summary>
    /// JSON 文件与对象复制辅助方法。
    /// </summary>
    public static class JsonFileHelper
    {
        /// <summary>
        /// 通过 JSON 序列化创建深拷贝，适合简单数据模型，不适合包含运行时事件的对象。
        /// </summary>
        public static T DeepClone<T>(this T source) where T : class
        {
            if (source == null)
                return null;

            string json = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// 按同名可写属性浅复制对象值。
        /// </summary>
        public static void CopyProperties<T>(this T source, T target) where T : class
        {
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                if (property.CanWrite)
                {
                    property.SetValue(target, property.GetValue(source));
                }
            }
        }

        /// <summary>
        /// 将 source 的 JSON 值填充到 target 中，保留 target 对象引用。
        /// </summary>
        public static void ApplyValuesFrom<T>(this T target, T source) where T : class
        {
            if (target == null || source == null)
                return;

            string json = JsonConvert.SerializeObject(source);
            var settings = new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            JsonConvert.PopulateObject(json, target, settings);
        }

        /// <summary>
        /// 打开保存对话框并将对象序列化为 JSON 文件。
        /// </summary>
        public static bool SaveToDialog<T>(this T data, string defaultFileName = "data.json", string title = "保存 JSON 数据")
        {
            if (data == null)
                return false;

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
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从 JSON 文件加载数据并填充到现有对象中，避免替换 ViewModel 正在持有的数据模型实例。
        /// </summary>
        public static bool LoadFromDialog<T>(this T targetObject, string title = "打开 JSON 数据") where T : class
        {
            if (targetObject == null)
                return false;

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
            catch
            {
                return false;
            }
        }
    }
}
