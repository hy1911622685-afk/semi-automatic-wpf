using MyAsset.Wpf;

namespace MyAsset.Wpf.Messaging
{
    public sealed class MessageBoxService : IMessageBoxService
    {
        private readonly string _logSource;
        private readonly string _logCategory;
        private readonly string _errorTitle;
        private readonly string _warningTitle;

        public MessageBoxService(
            string logSource = "MyAsset-wpf",
            string logCategory = "消息框",
            string errorTitle = "错误",
            string warningTitle = "警告")
        {
            _logSource = logSource;
            _logCategory = logCategory;
            _errorTitle = errorTitle;
            _warningTitle = warningTitle;
        }

        public void ShowError(string message)
        {
            RuntimeLogMessenger.Broadcast(_logSource, _logCategory, message);
            MyMessageBox.ShowError(message, _errorTitle);
        }

        public void ShowWarning(string message)
        {
            RuntimeLogMessenger.Broadcast(_logSource, _logCategory, message);
            MyMessageBox.ShowWarn(message, _warningTitle);
        }
    }
}
