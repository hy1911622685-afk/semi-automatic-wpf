using Microsoft.Win32;

namespace HKVision.Wpf.Services.Dialogs
{
    public sealed class FileDialogService : IFileDialogService
    {
        public string SelectSolutionPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "VM Sol File|*.sol*"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
        }
    }
}
