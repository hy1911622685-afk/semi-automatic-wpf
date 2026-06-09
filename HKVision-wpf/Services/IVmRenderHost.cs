using VMControls.Interface;

namespace HKVision.Wpf.Services
{
    public interface IVmRenderHost
    {
        IVmModule ModuleSource { get; set; }
    }
}
