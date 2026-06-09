using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyAsset.Wpf.ViewModels
{
    public partial class AssetToolViewModel : ObservableObject
    {
        [ObservableProperty]
        private string message = "资产工具库";

        [RelayCommand]
        private void ShowInfo()
        {
            MyMessageBox.ShowInfo(Message, "资产工具");
        }
    }
}
