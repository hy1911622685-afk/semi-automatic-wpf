using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;

namespace WaferMap.Wpf.Model
{
    /// <summary>
    /// 管理晶圆图中 Die 的显示颜色与测试结果 Bin 颜色。
    /// </summary>
    public class ColorManager
    {
        public const string PassedBinCommand = "1";
        public const string FailedBinCommand = "2";

        [JsonIgnore]
        private readonly Dictionary<string, BinDefinition> _stateRegistry =
            new Dictionary<string, BinDefinition>(StringComparer.Ordinal);

        private List<BinDefinition> _binDefinitions = new List<BinDefinition>();

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
            BinDefinitions = CreateDefaultDefinitions();
        }

        /// <summary>
        /// 当前注册的测试结果 Bin 定义。该列表会随晶圆图 JSON 一起保存。
        /// </summary>
        public List<BinDefinition> BinDefinitions
        {
            get => _binDefinitions;
            set
            {
                _binDefinitions = NormalizeDefinitions(value);
                RebuildStateRegistry();
            }
        }

        /// <summary>
        /// 获取当前所有测试结果 Bin 定义，包含系统默认 Bin 和用户自定义 Bin。
        /// </summary>
        public IEnumerable<BinDefinition> GetStateDefinitions()
        {
            EnsureRuntimeState();
            return _binDefinitions.Select(CloneStateDefinition).ToList();
        }

        /// <summary>
        /// 同步用户自定义 Bin；系统默认 Bin 不会被删除。
        /// </summary>
        public void SyncCustomStates(IEnumerable<BinDefinition> customDefinitions)
        {
            EnsureRuntimeState();

            var customItems = (customDefinitions ?? Enumerable.Empty<BinDefinition>())
                .Where(x => x != null && !x.IsSystemDefault && !string.IsNullOrWhiteSpace(x.BinCommand))
                .Select(x => CreateStateDefinition(x.BinCommand, x.Description, x.Color, false))
                .ToList();

            var customCommands = new HashSet<string>(
                customItems.Select(x => GetBinCommandKey(x.BinCommand)),
                StringComparer.Ordinal);

            _binDefinitions.RemoveAll(x =>
                !IsSystemDefaultBin(x.BinCommand) &&
                !customCommands.Contains(GetBinCommandKey(x.BinCommand)));

            foreach (var item in customItems)
            {
                UpsertState(item.BinCommand, item.Description, item.Color, false);
            }

            EnsureSystemDefaultStates();
            RebuildStateRegistry();
        }

        /// <summary>
        /// 添加或更新自定义 Bin；系统默认 Bin 只允许更新颜色。
        /// </summary>
        public void AddOrUpdateCustomState(string command, string description, Color color)
        {
            EnsureRuntimeState();
            UpsertState(command, description, color, false);
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
        /// 校验外部传入的 Bin 命令；仅已注册的 Bin 才会返回成功。
        /// </summary>
        public bool TryResolveBinCommand(string value, out string binCommand)
        {
            binCommand = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            EnsureRuntimeState();
            if (!_stateRegistry.TryGetValue(GetBinCommandKey(value), out var definition))
                return false;

            binCommand = definition.BinCommand;
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

            EnsureRuntimeState();
            if (_stateRegistry.TryGetValue(GetBinCommandKey(binCommand), out var stateDef))
            {
                color = stateDef.Color;
                return true;
            }

            color = default;
            return false;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            BinDefinitions = _binDefinitions;
        }

        private void UpsertState(string command, string description, Color color, bool isSystemDefault)
        {
            var definition = CreateStateDefinition(
                command,
                description,
                color,
                isSystemDefault || IsSystemDefaultBin(command));
            string commandKey = GetBinCommandKey(definition.BinCommand);

            int existingIndex = _binDefinitions.FindIndex(x =>
                string.Equals(GetBinCommandKey(x.BinCommand), commandKey, StringComparison.Ordinal));

            if (existingIndex >= 0 &&
                _binDefinitions[existingIndex].IsSystemDefault &&
                !definition.IsSystemDefault)
            {
                _binDefinitions[existingIndex].Color = definition.Color;
                RebuildStateRegistry();
                return;
            }

            if (existingIndex >= 0)
                _binDefinitions[existingIndex] = definition;
            else
                _binDefinitions.Add(definition);

            RebuildStateRegistry();
        }

        private void EnsureRuntimeState()
        {
            if (_binDefinitions == null)
                _binDefinitions = new List<BinDefinition>();

            EnsureSystemDefaultStates();

            if (_stateRegistry.Count == 0)
                RebuildStateRegistry();
        }

        private void EnsureSystemDefaultStates()
        {
            EnsureSystemDefaultState(_binDefinitions, PassedBinCommand, "测试成功", Color.Green);
            EnsureSystemDefaultState(_binDefinitions, FailedBinCommand, "测试失败", Color.Red);
        }

        private void RebuildStateRegistry()
        {
            _stateRegistry.Clear();

            foreach (var definition in _binDefinitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.BinCommand))
                    continue;

                _stateRegistry[GetBinCommandKey(definition.BinCommand)] = definition;
            }
        }

        private static List<BinDefinition> CreateDefaultDefinitions()
        {
            return new List<BinDefinition>
            {
                CreateStateDefinition(PassedBinCommand, "测试成功", Color.Green, true),
                CreateStateDefinition(FailedBinCommand, "测试失败", Color.Red, true)
            };
        }

        private static List<BinDefinition> NormalizeDefinitions(IEnumerable<BinDefinition> definitions)
        {
            var items = new List<BinDefinition>();

            foreach (var definition in definitions ?? Enumerable.Empty<BinDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.BinCommand))
                    continue;

                var item = CreateStateDefinition(
                    definition.BinCommand,
                    definition.Description,
                    definition.Color,
                    definition.IsSystemDefault || IsSystemDefaultBin(definition.BinCommand));

                int existingIndex = items.FindIndex(x =>
                    string.Equals(
                        GetBinCommandKey(x.BinCommand),
                        GetBinCommandKey(item.BinCommand),
                        StringComparison.Ordinal));

                if (existingIndex >= 0)
                    items[existingIndex] = item;
                else
                    items.Add(item);
            }

            EnsureSystemDefaultState(items, PassedBinCommand, "测试成功", Color.Green);
            EnsureSystemDefaultState(items, FailedBinCommand, "测试失败", Color.Red);
            return items;
        }

        private static void EnsureSystemDefaultState(
            List<BinDefinition> definitions,
            string command,
            string description,
            Color color)
        {
            string commandKey = GetBinCommandKey(command);
            int index = definitions.FindIndex(x =>
                string.Equals(GetBinCommandKey(x.BinCommand), commandKey, StringComparison.Ordinal));

            if (index < 0)
            {
                definitions.Add(CreateStateDefinition(command, description, color, true));
                return;
            }

            definitions[index].BinCommand = commandKey;
            definitions[index].IsSystemDefault = true;
            if (string.IsNullOrWhiteSpace(definitions[index].Description))
                definitions[index].Description = description;
            if (definitions[index].Color.IsEmpty)
                definitions[index].Color = color;
        }

        private static BinDefinition CreateStateDefinition(string command, string description, Color color, bool isSystemDefault)
        {
            return new BinDefinition
            {
                BinCommand = GetBinCommandKey(command),
                Description = description?.Trim(),
                Color = color,
                IsSystemDefault = isSystemDefault
            };
        }

        private static BinDefinition CloneStateDefinition(BinDefinition definition)
        {
            return CreateStateDefinition(
                definition.BinCommand,
                definition.Description,
                definition.Color,
                definition.IsSystemDefault);
        }

        private static bool IsSystemDefaultBin(string command)
        {
            string commandKey = GetBinCommandKey(command);
            return string.Equals(commandKey, PassedBinCommand, StringComparison.Ordinal) ||
                string.Equals(commandKey, FailedBinCommand, StringComparison.Ordinal);
        }

        private static string GetBinCommandKey(string command) => command?.Trim() ?? string.Empty;
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
