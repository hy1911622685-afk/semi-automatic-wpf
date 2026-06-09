using System.Windows;
using System.Windows.Controls;

namespace HKVision.Wpf.Views
{
    public partial class VisionHeadView : UserControl
    {
        public VisionHeadView()
        {
            if (Application.Current != null && Application.Current.Resources["Rotate270Transform"] == null)
            {
                Application.Current.Resources["Rotate270Transform"] = new System.Windows.Media.RotateTransform(-90);
            }

            InitializeComponent();
        }
    }
}
