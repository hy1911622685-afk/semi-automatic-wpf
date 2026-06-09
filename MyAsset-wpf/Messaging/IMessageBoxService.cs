namespace MyAsset.Wpf.Messaging
{
    public interface IMessageBoxService
    {
        void ShowError(string message);
        void ShowWarning(string message);
    }
}
