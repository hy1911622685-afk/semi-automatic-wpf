using MyAsset.Wpf.ViewModels;
using System.Windows.Controls;

namespace MyAsset.Wpf.Views
{
    public partial class AssetToolView : UserControl
    {
        public AssetToolView()
        {
            InitializeComponent();
            DataContext = new AssetToolViewModel();
        }
    }
}
