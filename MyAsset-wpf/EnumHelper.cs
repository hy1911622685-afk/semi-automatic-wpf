using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace MyAsset.Wpf
{
    public class EnumHelper
    {
        public static string GetEnumDescription(Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        public static List<KeyValuePair<string, string>> GetEnumList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => new KeyValuePair<string, string>(e.ToString(), GetEnumDescription(e)))
                .ToList();
        }

        public static T GetEnumFromString<T>(string value) where T : struct, Enum
        {
            return Enum.TryParse(value, out T result) ? result : default;
        }
    }
}
