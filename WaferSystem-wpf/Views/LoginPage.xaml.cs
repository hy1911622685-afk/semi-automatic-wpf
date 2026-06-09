using System;
using System.Windows.Controls;
using WaferSystem.Wpf.ViewModels;

namespace WaferSystem.Wpf.Views;

public partial class LoginPage : UserControl
{
    public event EventHandler LoginRequested;

    public LoginPage()
    {
        InitializeComponent();

        var viewModel = new LoginViewModel();
        viewModel.LoginRequested += (_, e) => LoginRequested?.Invoke(this, e);
        DataContext = viewModel;
    }
}
