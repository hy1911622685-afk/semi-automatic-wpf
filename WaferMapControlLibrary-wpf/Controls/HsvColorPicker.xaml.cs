using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WaferMap.Wpf.Controls
{
    public partial class HsvColorPicker : UserControl
    {
        public static readonly DependencyProperty SelectedBrushProperty =
            DependencyProperty.Register(
                nameof(SelectedBrush),
                typeof(Brush),
                typeof(HsvColorPicker),
                new FrameworkPropertyMetadata(
                    Brushes.Red,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedBrushChanged));

        private bool _isDraggingColor;
        private bool _isDraggingHue;
        private bool _isUpdatingBrush;
        private double _hue;
        private double _saturation = 1;
        private double _value = 1;

        public HsvColorPicker()
        {
            InitializeComponent();
            UpdateColorAreaBackground();
            Loaded += (_, _) => UpdateThumbs();
        }

        public Brush SelectedBrush
        {
            get => (Brush)GetValue(SelectedBrushProperty);
            set => SetValue(SelectedBrushProperty, value);
        }

        private static void OnSelectedBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (HsvColorPicker)d;
            if (picker._isUpdatingBrush)
                return;

            if (e.NewValue is SolidColorBrush brush)
                picker.ApplyColor(brush.Color);
        }

        private void ApplyColor(Color color)
        {
            ToHsv(color, out _hue, out _saturation, out _value);
            UpdateColorAreaBackground();
            UpdateThumbs();
        }

        private void ColorArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingColor = true;
            ColorArea.CaptureMouse();
            UpdateSaturationValue(e.GetPosition(ColorArea));
        }

        private void ColorArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingColor)
                UpdateSaturationValue(e.GetPosition(ColorArea));
        }

        private void ColorArea_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingColor = false;
            ColorArea.ReleaseMouseCapture();
        }

        private void ColorArea_MouseLeave(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isDraggingColor = false;
                ColorArea.ReleaseMouseCapture();
            }
        }

        private void HueTrack_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingHue = true;
            HueHost.CaptureMouse();
            UpdateHue(e.GetPosition(HueHost));
        }

        private void HueTrack_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingHue)
                UpdateHue(e.GetPosition(HueHost));
        }

        private void HueTrack_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingHue = false;
            HueHost.ReleaseMouseCapture();
        }

        private void HueTrack_MouseLeave(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isDraggingHue = false;
                HueHost.ReleaseMouseCapture();
            }
        }

        private void PickerPart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbs();
        }

        private void UpdateSaturationValue(Point point)
        {
            double width = Math.Max(1, ColorArea.ActualWidth);
            double height = Math.Max(1, ColorArea.ActualHeight);

            _saturation = Clamp(point.X / width, 0, 1);
            _value = 1 - Clamp(point.Y / height, 0, 1);

            UpdateSelectedBrush();
            UpdateThumbs();
        }

        private void UpdateHue(Point point)
        {
            double thumbRadius = HueThumb.Width / 2;
            double width = Math.Max(1, HueHost.ActualWidth - (thumbRadius * 2));
            _hue = Clamp((point.X - thumbRadius) / width, 0, 1) * 360;

            UpdateColorAreaBackground();
            UpdateSelectedBrush();
            UpdateThumbs();
        }

        private void UpdateSelectedBrush()
        {
            try
            {
                _isUpdatingBrush = true;
                SetCurrentValue(SelectedBrushProperty, new SolidColorBrush(FromHsv(_hue, _saturation, _value)));
            }
            finally
            {
                _isUpdatingBrush = false;
            }
        }

        private void UpdateColorAreaBackground()
        {
            if (ColorArea != null)
                ColorArea.Background = new SolidColorBrush(FromHsv(_hue, 1, 1));
        }

        private void UpdateThumbs()
        {
            if (ColorArea.ActualWidth > 0 && ColorArea.ActualHeight > 0)
            {
                Canvas.SetLeft(ColorThumb, (_saturation * ColorArea.ActualWidth) - (ColorThumb.Width / 2));
                Canvas.SetTop(ColorThumb, ((1 - _value) * ColorArea.ActualHeight) - (ColorThumb.Height / 2));
            }

            if (HueHost.ActualWidth > 0)
            {
                double thumbRadius = HueThumb.Width / 2;
                double width = Math.Max(1, HueHost.ActualWidth - (thumbRadius * 2));
                Canvas.SetLeft(HueThumb, thumbRadius + ((_hue / 360) * width) - (HueThumb.Width / 2));
                Canvas.SetTop(HueThumb, (HueHost.ActualHeight - HueThumb.Height) / 2);
            }
        }

        private static Color FromHsv(double hue, double saturation, double value)
        {
            hue = ((hue % 360) + 360) % 360;
            saturation = Clamp(saturation, 0, 1);
            value = Clamp(value, 0, 1);

            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60 % 2) - 1));
            double m = value - c;

            double r;
            double g;
            double b;

            if (hue < 60)
            {
                r = c;
                g = x;
                b = 0;
            }
            else if (hue < 120)
            {
                r = x;
                g = c;
                b = 0;
            }
            else if (hue < 180)
            {
                r = 0;
                g = c;
                b = x;
            }
            else if (hue < 240)
            {
                r = 0;
                g = x;
                b = c;
            }
            else if (hue < 300)
            {
                r = x;
                g = 0;
                b = c;
            }
            else
            {
                r = c;
                g = 0;
                b = x;
            }

            return Color.FromRgb(
                ToByte((r + m) * 255),
                ToByte((g + m) * 255),
                ToByte((b + m) * 255));
        }

        private static void ToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            if (delta == 0)
                hue = 0;
            else if (max == r)
                hue = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                hue = 60 * (((b - r) / delta) + 2);
            else
                hue = 60 * (((r - g) / delta) + 4);

            if (hue < 0)
                hue += 360;

            saturation = max == 0 ? 0 : delta / max;
            value = max;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static byte ToByte(double value)
        {
            value = Math.Round(Clamp(value, 0, 255));
            return (byte)value;
        }
    }
}
