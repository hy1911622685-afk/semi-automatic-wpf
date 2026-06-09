using HKVision.Wpf.Views;
using System.Windows;
using VM.Core;

namespace HKVision.Wpf.Services.Dialogs
{
    public sealed class ModuleParamDialogService : IModuleParamDialogService
    {
        public bool Show(VmModule module)
        {
            var window = new ParamConfigWindow(module)
            {
                Owner = Application.Current?.MainWindow
            };

            bool? result = window.ShowDialog();
            return result != false;
        }
    }
}
