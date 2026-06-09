using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WaferMap.Wpf.Infrastructure;

namespace WaferMap.Wpf.Model
{
    /// <summary>
    /// 管理晶圆图中 Die 的显示颜色与测试结果 Bin 颜色。
    /// </summary>
    public class ColorManager
    {
        public const string PassedBinCommand = "Bin1";
        public const string FailedBinCommand = "Bin2";

        private readonly Dictionary<string, BinDefinition> _stateRegistry =
            new Dictionary<string, BinDefinition>(StringComparer.OrdinalIgnoreCase);

        private const string StateCommandPrefix = "Bin";

        public Color EdgeEnabledColor = Color.Gainsboro;
        public Color DefaultColor = Color.LightGray;
        public Color DisabledColor = Color.DimGray;
        public Color SelectedForTestColor = Color.Orange;
        public Color RegionStrokeColor = Color.Chocolate;
        public Color SelectStrokeColor = Color.SkyBlue;
        public Color SelectFillColor = Color.Transparent;
        public Color OutlineColor = Color.FromArgb(255, 171, 168, 217);
        public Color HomeTextColor = Color.FromArgb(80, 80, 80);

        public ColorManager()
        {
            RegisterState(PassedBinCommand, "测试成功", Color.Green, true);
            RegisterState(FailedBinCommand, "测试失败", Color.Red, true);
        }

        /// <summary>
        /// 获取当前所有测试结果 Bin 定义，包含系统默认 Bin 和用户自定义 Bin。
        /// </summary>
        public IEnumerable<BinDefinition> GetStateDefinitions() => _stateRegistry.Values;

        /// <summary>
        /// 同步用户自定义 Bin；系统默认 Bin 不会被删除。
        /// </summary>
        public void SyncCustomStates(IEnumerable<BinDefinition> customDefinitions)
        {
            var customItems = customDefinitions
                .Where(x => x != null && !x.IsSystemDefault && !string.IsNullOrWhiteSpace(x.BinCommand))
                .ToList();

            var customCommands = new HashSet<string>(
                customItems.Select(x => NormalizeBinCommand(x.BinCommand)),
                StringComparer.OrdinalIgnoreCase);

            var removedCommands = _stateRegistry
                .Where(x => !x.Value.IsSystemDefault && !customCommands.Contains(x.Key))
                .Select(x => x.Key)
                .ToList();

            foreach (string command in removedCommands)
                _stateRegistry.Remove(command);

            foreach (var item in customItems)
            {
                string command = NormalizeBinCommand(item.BinCommand);
                _stateRegistry[command] = new BinDefinition
                {
                    BinCommand = command,
                    Description = item.Description?.Trim(),
                    Color = item.Color,
                    IsSystemDefault = false
                };
            }
        }

        /// <summary>
        /// 注册 Bin 定义，并统一将指令规范化为 BinN 格式。
        /// </summary>
        private void RegisterState(string command, string description, Color color, bool isSystemDefault)
        {
            string normalized = NormalizeBinCommand(command);
            _stateRegistry[normalized] = new BinDefinition
            {
                BinCommand = normalized,
                Description = description,
                Color = color,
                IsSystemDefault = isSystemDefault
            };
        }

        /// <summary>
        /// 添加或更新自定义 Bin；系统默认 Bin 只允许更新颜色。
        /// </summary>
        public void AddOrUpdateCustomState(string command, string description, Color color)
        {
            string normalized = NormalizeBinCommand(command);

            if (_stateRegistry.TryGetValue(normalized, out var existing))
            {
                if (existing.IsSystemDefault)
                {
                    existing.Color = color;
                }
                else
                {
                    existing.BinCommand = normalized;
                    existing.Description = description;
                    existing.Color = color;
                }
            }
            else
            {
                RegisterState(normalized, description, color, false);
            }
        }

        /// <summary>
        /// 获取 Die 的最终显示颜色；优先级为禁用、测试结果 Bin、待测、边缘、默认。
        /// </summary>
        public Color GetFinalRenderColor(DieAttributes attrs, bool isSelectedForTest, string binCommand)
        {
            bool isEnabled = (attrs & DieAttributes.IsEnabled) != 0;
            bool isEdge = (attrs & DieAttributes.IsEdge) != 0;

            if (!isEnabled)
                return DisabledColor;

            if (TryGetBinColor(binCommand, out var binColor))
                return binColor;

            if (isSelectedForTest)
                return SelectedForTestColor;

            if (isEdge)
                return EdgeEnabledColor;

            return DefaultColor;
        }

        /// <summary>
        /// 校验并规范化外部传入的 Bin 指令，仅已注册 Bin 才会返回成功。
        /// </summary>
        public bool TryResolveBinCommand(string value, out string binCommand)
        {
            binCommand = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = NormalizeBinCommand(value);
            if (!_stateRegistry.ContainsKey(normalized))
                return false;

            binCommand = normalized;
            return true;
        }

        /// <summary>
        /// 获取测试结果 Bin 的颜色；待测和跳过不是 Bin，因此不会在这里解析。
        /// </summary>
        public bool TryGetBinColor(string binCommand, out Color color)
        {
            if (string.IsNullOrWhiteSpace(binCommand))
            {
                color = default;
                return false;
            }

            string normalized = NormalizeBinCommand(binCommand);
            if (_stateRegistry.TryGetValue(normalized, out var stateDef))
            {
                color = stateDef.Color;
                return true;
            }

            color = default;
            return false;
        }

        /// <summary>
        /// 将 1、Bin1 等输入统一为 Bin1 格式。
        /// </summary>
        private static string NormalizeBinCommand(string command)
        {
            string trimmed = command?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            if (trimmed.StartsWith(StateCommandPrefix, StringComparison.OrdinalIgnoreCase))
                return StateCommandPrefix + trimmed.Substring(StateCommandPrefix.Length).Trim();

            return StateCommandPrefix + trimmed;
        }
    }

    /// <summary>
    /// 单个测试结果 Bin 的显示定义。
    /// </summary>
    public class BinDefinition
    {
        public string BinCommand { get; set; }
        public string Description { get; set; }
        public Color Color { get; set; }
        public bool IsSystemDefault { get; set; }
    }
}
