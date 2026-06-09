using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace MotionCard.Wpf.ViewModels
{
    public sealed partial class IoPortItemViewModel : ObservableObject
    {
        private readonly Action<IoPortItemViewModel> _toggleRequested;

        public IoPortItemViewModel(
            int cardIndex,
            int bitIndex,
            bool isOutput,
            string content,
            Action<IoPortItemViewModel> toggleRequested = null)
        {
            CardIndex = cardIndex;
            BitIndex = bitIndex;
            IsOutput = isOutput;
            Content = content;
            Header = $"{(isOutput ? "O" : "I")}{bitIndex}";
            _toggleRequested = toggleRequested;
        }

        public int CardIndex { get; }
        public int BitIndex { get; }
        public bool IsOutput { get; }
        public bool IsInput => !IsOutput;
        public string Header { get; }
        public string Content { get; }
        public string DirectionText => IsOutput ? "输出" : "输入";
        public string StateText => IsActive ? "ON" : "OFF";

        [ObservableProperty]
        private bool isActive;

        partial void OnIsActiveChanged(bool value)
        {
            OnPropertyChanged(nameof(StateText));
        }

        [RelayCommand]
        private void Toggle()
        {
            if (!IsOutput)
                return;

            _toggleRequested?.Invoke(this);
        }
    }

    public sealed class IoBoardViewModel
    {
        public IoBoardViewModel(int cardIndex, string title)
        {
            CardIndex = cardIndex;
            Title = title;
        }

        public int CardIndex { get; }
        public string Title { get; }
        public ObservableCollection<IoPortItemViewModel> Inputs { get; } = new ObservableCollection<IoPortItemViewModel>();
        public ObservableCollection<IoPortItemViewModel> Outputs { get; } = new ObservableCollection<IoPortItemViewModel>();
    }

    public sealed partial class IoListenViewModel : ObservableObject
    {
        [ObservableProperty]
        private string statusText = "IO监听未启动";

        [ObservableProperty]
        private bool isConnected;

        public ObservableCollection<IoBoardViewModel> Boards { get; } = new ObservableCollection<IoBoardViewModel>();
    }
}
