using System;

namespace WaferMap.Wpf.Model
{
    /// <summary>
    /// 晶圆图中单颗 Die 的运行时与序列化状态。
    /// </summary>
    public class DieModel
    {
        private DieAttributes attrs = DieAttributes.IsEnabled;
        private bool isSelectedForTest;
        private string binCommand;

        public int Index { get; set; } = -1;
        public int GridX { get; set; }
        public int GridY { get; set; }
        public (double X, double Y)? PhysicalPosition { get; set; }
        public DieAttributes Attrs
        {
            get => attrs;
            set
            {
                attrs = value;
                if (!IsEnabled)
                    ClearEnabledOnlyState();
            }
        }

        /// <summary>
        /// 是否加入测试/复测队列；已有测试结果不会自动清除此标记。
        /// </summary>
        public bool IsSelectedForTest
        {
            get => IsEnabled && isSelectedForTest;
            set => isSelectedForTest = IsEnabled && value;
        }

        /// <summary>
        /// 仅保存实际测试结果 Bin 指令；待测/跳过状态不写入这里。
        /// </summary>
        public string BinCommand
        {
            get => IsEnabled ? binCommand : null;
            set => binCommand = IsEnabled && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }

        /// <summary>
        /// 是否启用该 Die，未启用的 Die 不参与常规操作与测试队列。
        /// </summary>
        public bool IsEnabled
        {
            get => (attrs & DieAttributes.IsEnabled) != 0;
            set
            {
                if (value)
                    attrs |= DieAttributes.IsEnabled;
                else
                {
                    attrs &= ~DieAttributes.IsEnabled;
                    ClearEnabledOnlyState();
                }
            }
        }

        /// <summary>
        /// 是否为边缘 Die；边缘 Die 仍可启用，但显示和默认待测规则会区别处理。
        /// </summary>
        public bool IsEdge
        {
            get => (attrs & DieAttributes.IsEdge) != 0;
            set
            {
                if (value)
                    attrs |= DieAttributes.IsEdge;
                else
                    attrs &= ~DieAttributes.IsEdge;
            }
        }

        public bool HasBin => !string.IsNullOrWhiteSpace(BinCommand);

        /// <summary>
        /// 导航与移动顺序使用的队列判断；即使被标记待测，未启用 Die 也会被排除。
        /// </summary>
        public bool IsInTestQueue => IsEnabled && IsSelectedForTest;

        /// <summary>
        /// 禁用 Die 时同时移出测试队列，并清空已有测试结果。
        /// </summary>
        public void Disable()
        {
            IsEnabled = false;
        }

        /// <summary>
        /// 重新启用 Die，但不改变待测状态和测试结果。
        /// </summary>
        public void Enable()
        {
            IsEnabled = true;
        }

        /// <summary>
        /// 从测试队列移除；不会清空已有测试结果。
        /// </summary>
        public void SkipTest()
        {
            IsSelectedForTest = false;
        }

        /// <summary>
        /// 加入测试队列；已有 Bin 结果会保留，便于整片复测。
        /// </summary>
        public void SelectForTest()
        {
            if (IsEnabled)
                IsSelectedForTest = true;
        }

        /// <summary>
        /// 写入或清空实际测试结果 Bin 指令。
        /// </summary>
        public void ApplyBin(string binCommand)
        {
            BinCommand = string.IsNullOrWhiteSpace(binCommand) ? null : binCommand;
        }

        private void ClearEnabledOnlyState()
        {
            isSelectedForTest = false;
            binCommand = null;
        }

    }
}
