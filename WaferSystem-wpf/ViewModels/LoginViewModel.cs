using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyAsset.Wpf;

namespace WaferSystem.Wpf.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string username = "admin";

    [ObservableProperty]
    private string password = "admin";

    [ObservableProperty]
    private bool rememberMe = true;

    [ObservableProperty]
    private bool showPassword;

    [ObservableProperty]
    private bool isLoading;

    public event EventHandler LoginRequested;

    public bool IsNotLoading => !IsLoading;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
        LoginCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotLoading))]
    private async Task LoginAsync()
    {
        IsLoading = true;

        try
        {
            await Task.Delay(900);
            LoginRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ForgotPassword()
    {
        MyMessageBox.ShowInfo("当前登录逻辑已简化，可直接点击登录进入系统。", "登录提示");
    }

    [RelayCommand]
    private void Help()
    {
        MyMessageBox.ShowInfo("请联系设备管理员或技术支持人员。", "技术支持");
    }
}
