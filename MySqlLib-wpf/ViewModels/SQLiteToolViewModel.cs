using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;

namespace MySqlLib.Wpf.ViewModels
{
    public partial class SQLiteToolViewModel : ObservableObject
    {
        [ObservableProperty]
        private string databasePath = Path.Combine(Environment.CurrentDirectory, "data.db");

        [ObservableProperty]
        private string statusText = "未连接";

        [RelayCommand]
        private void CheckDatabase()
        {
            try
            {
                using var helper = new SQLiteHelper(DatabasePath);
                helper.OpenConnection();
                StatusText = $"连接成功，表数量：{helper.GetTableNames().Count}";
                helper.CloseConnection();
            }
            catch (Exception ex)
            {
                StatusText = $"连接失败：{ex.Message}";
            }
        }
    }
}
