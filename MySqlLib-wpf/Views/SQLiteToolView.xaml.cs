using MySqlLib.Wpf.ViewModels;
using System.Windows.Controls;

namespace MySqlLib.Wpf.Views
{
    public partial class SQLiteToolView : UserControl
    {
        public SQLiteToolView()
        {
            InitializeComponent();
            DataContext = new SQLiteToolViewModel();
        }
    }
}
