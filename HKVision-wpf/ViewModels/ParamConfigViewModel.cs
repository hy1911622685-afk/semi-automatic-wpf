using CommunityToolkit.Mvvm.ComponentModel;
using VM.Core;

namespace HKVision.Wpf.ViewModels
{
    public partial class ParamConfigViewModel : ObservableObject
    {
        [ObservableProperty]
        private VmModule module;

        public ParamConfigViewModel(VmModule module)
        {
            Module = module;
        }
    }
}
