using System.Windows.Media;

namespace WaferMap.Wpf.ViewModels
{
    public class BinTestStatisticViewModel
    {
        public string BinCommand { get; set; }
        public string Description { get; set; }
        public int Count { get; set; }
        public double PercentOfTested { get; set; }
        public Brush ColorBrush { get; set; }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Description)
                ? BinCommand
                : $"{BinCommand} {Description}";

        public string SummaryText => $"{Count} ({PercentOfTested:F1}%)";
    }
}
