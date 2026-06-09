using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Media;
using WaferMap.Wpf.Model;
using DrawingColor = System.Drawing.Color;
using DrawingColorTranslator = System.Drawing.ColorTranslator;

namespace WaferMap.Wpf.ViewModels
{
    /// <summary>
    /// Bin 配置界面使用的显示模型，负责在 Hex、Brush、RGB 三种颜色表示之间同步。
    /// </summary>
    public partial class BinDefinitionViewModel : ObservableObject
    {
        // 防止 Hex、Brush、RGB 属性互相更新时产生递归触发。
        private bool _isSyncingColor;

        [ObservableProperty]
        private int displayIndex;

        [ObservableProperty]
        private string binCommand;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private string colorHex;

        [ObservableProperty]
        private Brush colorBrush;

        [ObservableProperty]
        private int colorR;

        [ObservableProperty]
        private int colorG;

        [ObservableProperty]
        private int colorB;

        [ObservableProperty]
        private bool isSystemDefault;

        public BinDefinitionViewModel()
        {
        }

        public BinDefinitionViewModel(BinDefinition definition)
        {
            BinCommand = definition.BinCommand;
            Description = definition.Description;
            ColorHex = DrawingColorTranslator.ToHtml(definition.Color);
            IsSystemDefault = definition.IsSystemDefault;
        }

        /// <summary>
        /// 将界面颜色转换为 System.Drawing.Color；非法颜色输入时返回默认颜色。
        /// </summary>
        public DrawingColor GetColorOrDefault(DrawingColor defaultColor)
        {
            try
            {
                return DrawingColorTranslator.FromHtml(ColorHex);
            }
            catch
            {
                return defaultColor;
            }
        }

        partial void OnColorHexChanged(string value)
        {
            if (_isSyncingColor)
                return;

            try
            {
                _isSyncingColor = true;
                var color = (Color)ColorConverter.ConvertFromString(value);
                ColorBrush = new SolidColorBrush(color);
                UpdateRgb(color);
            }
            catch
            {
                ColorBrush = Brushes.Transparent;
            }
            finally
            {
                _isSyncingColor = false;
            }
        }

        partial void OnColorBrushChanged(Brush value)
        {
            if (_isSyncingColor || value is not SolidColorBrush brush)
                return;

            _isSyncingColor = true;
            ApplyColor(brush.Color);
            _isSyncingColor = false;
        }

        partial void OnColorRChanged(int value)
        {
            ApplyRgbFromChannels();
        }

        partial void OnColorGChanged(int value)
        {
            ApplyRgbFromChannels();
        }

        partial void OnColorBChanged(int value)
        {
            ApplyRgbFromChannels();
        }

        private void ApplyRgbFromChannels()
        {
            if (_isSyncingColor)
                return;

            // RGB 文本框允许短暂输入越界值，最终统一裁剪到 byte 范围。
            _isSyncingColor = true;
            ApplyColor(Color.FromRgb(ToByte(ColorR), ToByte(ColorG), ToByte(ColorB)));
            _isSyncingColor = false;
        }

        private void ApplyColor(Color color)
        {
            ColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            ColorBrush = new SolidColorBrush(color);
            UpdateRgb(color);
        }

        private void UpdateRgb(Color color)
        {
            ColorR = color.R;
            ColorG = color.G;
            ColorB = color.B;
        }

        private static byte ToByte(int value)
        {
            if (value < 0)
                return 0;
            if (value > 255)
                return 255;
            return (byte)value;
        }
    }
}
