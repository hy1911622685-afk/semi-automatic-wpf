using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WaferSystem.Wpf.Controls;

/// <summary>
/// 轨道星座图用户控件 – 还原 React 版本的 OrbitConstellation 组件
/// 包含：中心发光球体、两圈旋转轨道环、8个功能节点、连接线、脉冲动画
/// </summary>
public partial class OrbitConstellation : UserControl
{
    // 画布中心与轨道半径（匹配 React 原版 viewBox 0 0 440 440）
    private const double Cx = 220;
    private const double Cy = 220;
    private const double R1 = 135; // 外圈
    private const double R2 = 82;  // 内圈

    // 节点数据
    private static readonly OrbitNode[] Nodes =
    [
        new("示波器",    "M2,12 C4,4 8,4 12,12 C16,20 20,20 22,12",  0,   1), // Waves
        new("运动控制",  "M12,2 A10,10 0 1,1 11.99,2 M12,8 A4,4 0 1,1 11.99,8", 45,  2), // Cog
        new("网络分析仪","M12,2 A10,10 0 0,1 12,22 M12,2 A10,10 0 0,0 12,22 M2,12 L22,12", 90,  1), // Radio
        new("晶圆映射",  "M3,6 L9,3 L21,3 L21,15 L15,18 L3,18Z M3,6 L15,6 L15,18 M15,6 L21,3", 135, 2), // Map
        new("源表/数表", "M13,2 L3,14 L12,14 L11,22 L21,10 L12,10Z", 180, 1), // Zap
        new("系统集成",  "M4,4 L20,4 L20,20 L4,20Z M8,4 L8,20 M4,8 L20,8", 225, 2), // Cpu
        new("信号源",    "M22,12 L18,12 L15,3 L12,21 L9,3 L6,12 L2,12", 270, 1), // Activity
        new("视觉采集",  "M3,3 L21,3 L21,21 L3,21Z M3,7 L21,7 M12,3 L12,7", 315, 1), // Camera
    ];

    public OrbitConstellation()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawConstellation();
    }

    private void DrawConstellation()
    {
        var canvas = OrbitCanvas;
        canvas.Children.Clear();

        // 1. 大范围环境光晕
        DrawAmbientGlow(canvas);

        // 2. 轨道环（虚线，带旋转动画）
        DrawOrbitRing(canvas, R1, Color.FromArgb(0x46, 0xB9, 0xDD, 0xE4), 1.0, "4 6", 120, false);
        DrawOrbitRing(canvas, R2, Color.FromArgb(0x42, 0x77, 0xB7, 0xD4), 1.0, "3 5", 90, true);

        // 3. 连接线（从中心到各节点）
        DrawConnectingLines(canvas);

        // 4. 节点上的小圆点
        DrawNodeDots(canvas);

        // 5. 中心发光球体
        DrawCenterSphere(canvas);

        // 6. 脉冲动画
        DrawPulseRings(canvas);

        // 7. 中心 "S" 字母
        DrawCenterLogo(canvas);

        // 8. 节点图标和标签
        DrawNodeLabels(canvas);
    }

    /// <summary>大范围环境光晕</summary>
    private void DrawAmbientGlow(Canvas canvas)
    {
        var glow = new Ellipse
        {
            Width = 200,
            Height = 200,
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x54, 0x77, 0xB7, 0xD4), 0),
                    new GradientStop(Color.FromArgb(0x28, 0x1D, 0x6C, 0x9C), 0.4),
                    new GradientStop(Color.FromArgb(0x00, 0x1D, 0x6C, 0x9C), 1.0),
                }
            }
        };
        Canvas.SetLeft(glow, Cx - 100);
        Canvas.SetTop(glow, Cy - 100);
        canvas.Children.Add(glow);
    }

    /// <summary>绘制虚线轨道环并添加旋转动画</summary>
    private void DrawOrbitRing(Canvas canvas, double radius, Color strokeColor, double strokeWidth,
        string dashPattern, double durationSeconds, bool reverse)
    {
        var ring = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = strokeWidth,
            Fill = Brushes.Transparent,
            StrokeDashArray = ParseDashArray(dashPattern),
            RenderTransformOrigin = new Point(0.5, 0.5),
        };

        var rotateTransform = new RotateTransform(0);
        ring.RenderTransform = rotateTransform;

        Canvas.SetLeft(ring, Cx - radius);
        Canvas.SetTop(ring, Cy - radius);
        canvas.Children.Add(ring);

        // 旋转动画
        var animation = new DoubleAnimation
        {
            From = reverse ? 360 : 0,
            To = reverse ? 0 : 360,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            RepeatBehavior = RepeatBehavior.Forever,
        };
        rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    /// <summary>绘制从中心到各节点的连接线</summary>
    private void DrawConnectingLines(Canvas canvas)
    {
        foreach (var node in Nodes)
        {
            double r = node.Ring == 1 ? R1 : R2;
            double rad = node.Angle * Math.PI / 180;
            double nx = Cx + r * Math.Cos(rad);
            double ny = Cy + r * Math.Sin(rad);

            var line = new Line
            {
                X1 = Cx,
                Y1 = Cy,
                X2 = nx,
                Y2 = ny,
                Stroke = new SolidColorBrush(node.Ring == 1
                    ? Color.FromArgb(0x46, 0xB9, 0xDD, 0xE4)
                    : Color.FromArgb(0x3C, 0x77, 0xD4, 0xCC)),
                StrokeThickness = node.Ring == 1 ? 1.5 : 1.0,
                Effect = new BlurEffect { Radius = 2.5 },
            };
            canvas.Children.Add(line);
        }
    }

    /// <summary>节点位置上的小圆点</summary>
    private void DrawNodeDots(Canvas canvas)
    {
        foreach (var node in Nodes)
        {
            double r = node.Ring == 1 ? R1 : R2;
            double rad = node.Angle * Math.PI / 180;
            double nx = Cx + r * Math.Cos(rad);
            double ny = Cy + r * Math.Sin(rad);

            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(node.Ring == 1
                    ? Color.FromArgb(0xB8, 0xDD, 0xEE, 0xF7)
                    : Color.FromArgb(0xA6, 0x77, 0xD4, 0xCC)),
                Effect = new BlurEffect { Radius = 2 },
            };
            Canvas.SetLeft(dot, nx - 3);
            Canvas.SetTop(dot, ny - 3);
            canvas.Children.Add(dot);
        }
    }

    /// <summary>中心发光球体</summary>
    private void DrawCenterSphere(Canvas canvas)
    {
        // 外圈发光
        var outerGlow = new Ellipse
        {
            Width = 64,
            Height = 64,
            Fill = new SolidColorBrush(Color.FromArgb(0x42, 0x1D, 0x6C, 0x9C)),
            Stroke = new SolidColorBrush(Color.FromArgb(0xCC, 0xB9, 0xDD, 0xE4)),
            StrokeThickness = 1.5,
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0xFF, 0x77, 0xB7, 0xD4),
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 0.5,
            },
        };
        Canvas.SetLeft(outerGlow, Cx - 32);
        Canvas.SetTop(outerGlow, Cy - 32);
        canvas.Children.Add(outerGlow);

        // 内圈发光
        var innerGlow = new Ellipse
        {
            Width = 40,
            Height = 40,
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0xE6, 0xDD, 0xEE, 0xF7), 0),
                    new GradientStop(Color.FromArgb(0x50, 0x1D, 0x6C, 0x9C), 1.0),
                }
            },
        };
        Canvas.SetLeft(innerGlow, Cx - 20);
        Canvas.SetTop(innerGlow, Cy - 20);
        canvas.Children.Add(innerGlow);
    }

    /// <summary>脉冲扩散环动画</summary>
    private void DrawPulseRings(Canvas canvas)
    {
        DrawSinglePulse(canvas, Color.FromArgb(0x78, 0xB9, 0xDD, 0xE4), 1.5, TimeSpan.Zero);
        DrawSinglePulse(canvas, Color.FromArgb(0x58, 0x77, 0xB7, 0xD4), 1.0, TimeSpan.FromSeconds(1.5));
    }

    private void DrawSinglePulse(Canvas canvas, Color strokeColor, double strokeWidth, TimeSpan beginTime)
    {
        var pulse = new Ellipse
        {
            Width = 64,
            Height = 64,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = strokeWidth,
            Fill = Brushes.Transparent,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };

        var scaleTransform = new ScaleTransform(1, 1);
        pulse.RenderTransform = scaleTransform;

        Canvas.SetLeft(pulse, Cx - 32);
        Canvas.SetTop(pulse, Cy - 32);
        canvas.Children.Add(pulse);

        // 缩放动画 (从 1x 到 ~2x)
        var scaleXAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 2.03,
            Duration = TimeSpan.FromSeconds(3),
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = beginTime,
        };
        var scaleYAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 2.03,
            Duration = TimeSpan.FromSeconds(3),
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = beginTime,
        };

        // 透明度动画
        var opacityAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(3),
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = beginTime,
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        pulse.BeginAnimation(OpacityProperty, opacityAnim);
    }

    /// <summary>中心 "S" 字母</summary>
    private void DrawCenterLogo(Canvas canvas)
    {
        var text = new TextBlock
        {
            Text = "S",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0xFF, 0x77, 0xB7, 0xD4),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.6,
            },
        };

        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(text, Cx - text.DesiredSize.Width / 2);
        Canvas.SetTop(text, Cy - text.DesiredSize.Height / 2);
        canvas.Children.Add(text);
    }

    /// <summary>节点图标圆圈 + 标签文字</summary>
    /// <remarks>
    /// 模仿 React 版本：图标+标签组合整体居中于节点位置
    /// React 使用 flex-col items-center gap-1.5 + transform: translate(-50%, -50%)
    /// </remarks>
    private void DrawNodeLabels(Canvas canvas)
    {
        foreach (var node in Nodes)
        {
            double r = node.Ring == 1 ? R1 : R2;
            double rad = node.Angle * Math.PI / 180;
            double nx = Cx + r * Math.Cos(rad);
            double ny = Cy + r * Math.Sin(rad);
            bool isOuter = node.Ring == 1;

            double iconSize = isOuter ? 44 : 36;
            double iconInner = isOuter ? 18 : 14;
            double gap = 6; // gap-1.5 = 6px

            // 创建一个 StackPanel 容器，包含图标和标签，整体居中于节点位置
            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            // 图标圆圈背景
            var iconBorder = new Border
            {
                Width = iconSize,
                Height = iconSize,
                CornerRadius = new CornerRadius(iconSize / 2),
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x15, 0x30, 0x47)),
                BorderBrush = new SolidColorBrush(isOuter
                    ? Color.FromArgb(0xD0, 0xDD, 0xEE, 0xF7)
                    : Color.FromArgb(0xB8, 0x77, 0xD4, 0xCC)),
                BorderThickness = new Thickness(1.2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = isOuter
                        ? Color.FromArgb(0xFF, 0xB9, 0xDD, 0xE4)
                        : Color.FromArgb(0xFF, 0x77, 0xD4, 0xCC),
                    BlurRadius = isOuter ? 18 : 14,
                    ShadowDepth = 0,
                    Opacity = isOuter ? 0.34 : 0.28,
                },
                Child = new Path
                {
                    Data = Geometry.Parse(node.IconPath),
                    Stroke = new SolidColorBrush(isOuter
                        ? Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)
                        : Color.FromArgb(0xFF, 0xDD, 0xEE, 0xF7)),
                    StrokeThickness = 1.5,
                    Fill = Brushes.Transparent,
                    Stretch = Stretch.Uniform,
                    Width = iconInner,
                    Height = iconInner,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };

            // 标签文字
            var label = new TextBlock
            {
                Text = node.Label,
                FontSize = 10.9,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(isOuter
                    ? Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)
                    : Color.FromArgb(0xE8, 0xDD, 0xEE, 0xF7)),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, gap, 0, 0),
            };

            container.Children.Add(iconBorder);
            container.Children.Add(label);

            // 测量容器尺寸以实现居中定位
            container.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double totalWidth = container.DesiredSize.Width;
            double totalHeight = container.DesiredSize.Height;

            Canvas.SetLeft(container, nx - totalWidth / 2);
            Canvas.SetTop(container, ny - totalHeight / 2);
            canvas.Children.Add(container);
        }
    }

    private static DoubleCollection ParseDashArray(string pattern)
    {
        var parts = pattern.Split(' ');
        var collection = new DoubleCollection();
        foreach (var p in parts)
        {
            if (double.TryParse(p, out double val))
                collection.Add(val);
        }
        return collection;
    }

    /// <summary>轨道节点数据</summary>
    private record OrbitNode(string Label, string IconPath, double Angle, int Ring);
}
