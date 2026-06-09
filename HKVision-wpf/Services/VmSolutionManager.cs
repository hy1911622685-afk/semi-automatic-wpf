using HKVision.Wpf.Model;
using HKVision.Wpf.Services.Dialogs;
using IMVSFastFeatureMatchModuCs;
using MvCameraControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using VM.Core;
using VM.PlatformSDKCS;

namespace HKVision.Wpf.Services
{
    public class VmSolutionManager
    {
        private const int DefaultContinuousRunIntervalMs = 33;
        private const string RealtimeModuleName = "图像源";
        private const string MatchModuleName = "快速匹配";

        private readonly VisionDataModel _dataModel;
        private readonly IFileDialogService _fileDialogService;
        private readonly IModuleParamDialogService _moduleParamDialogService;

        private IVmRenderHost _renderHost;


        public bool IsConnect { get; private set; }
        public VmProcedure CurrentProcess { get; private set; }
        public VmModule CurrentModule { get; private set; }
        public List<string> ProcessNameList { get; } = new List<string>();
        public List<string> ModuleNameList { get; } = new List<string>();

        public VmSolutionManager(
            VisionDataModel dataModel,
            IFileDialogService fileDialogService = null,
            IModuleParamDialogService moduleParamDialogService = null)
        {
            _dataModel = dataModel;
            _fileDialogService = fileDialogService ?? new FileDialogService();
            _moduleParamDialogService = moduleParamDialogService ?? new ModuleParamDialogService();
        }

        public void BindRenderHost(IVmRenderHost renderHost)
        {
            _renderHost = renderHost;
        }

        public Result LookForDevice()
        {
            int result = DeviceEnumerator.EnumDevices(
                DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice,
                out List<IDeviceInfo> deviceInfoList);

            if (result != 0)
                return Result.Fail("设备查找失败，请检查设备链接及网络设置");

            return deviceInfoList.Count > 0 ? Result.Success() : Result.Fail("未找到可链接设备");
        }

        public Result LoadSolution()
        {
            string solPath = Path.Combine(Environment.CurrentDirectory, "ConfigFile", "hikVision.sol");
            return LoadProject(solPath);
        }

        public string SelectProject()
        {
            return _fileDialogService.SelectSolutionPath();
        }

        public Result LoadProject(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
                return Result.Fail("方案文件不存在或路径为空");

            ProcessNameList.Clear();

            try
            {
                VmSolution.Load(projectPath, string.Empty);
                var processList = VmSolution.Instance.GetAllProcedureList();
                if (processList.nNum <= 0)
                {
                    IsConnect = false;
                    return Result.Fail("没有可加载的流程");
                }

                for (int i = 0; i < processList.nNum; i++)
                    ProcessNameList.Add(processList.astProcessInfo[i].strProcessName);

                IsConnect = true;
                return Result.Success();
            }
            catch (VmException ex)
            {
                IsConnect = false;
                return Result.Fail($"视觉底层异常---{ex.Message}");
            }
            catch (Exception ex)
            {
                IsConnect = false;
                return Result.Fail($"未知异常--- {ex.Message}");
            }
        }

        public Result LoadVmProcess(int processIndex)
        {
            if (ProcessNameList.Count == 0)
                return Result.Fail("流程列表为空");

            return LoadVmProcess(ProcessNameList[processIndex % ProcessNameList.Count]);
        }

        public Result LoadVmModule(int moduleIndex)
        {
            if (CurrentProcess == null)
                return Result.Fail("请先绑定流程");
            if (ModuleNameList.Count == 0)
                return Result.Fail("模块列表为空");

            return LoadVmModule(ModuleNameList[moduleIndex % ModuleNameList.Count]);
        }

        public Result<VmModule> FindModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
                return Result<VmModule>.Fail("请明确需要查找的模块名称");
            if (CurrentProcess == null)
                return Result<VmModule>.Fail("请先绑定流程");

            var module = CurrentProcess.Modules.FirstOrDefault(o => o.Name.Contains(moduleName));
            return module is null
                ? Result<VmModule>.Fail($"未找到指定模块- {moduleName}")
                : Result<VmModule>.Success(module);
        }

        public Result LoadDefaultModule()
        {
            var result = LoadVmModule(RealtimeModuleName);
            if (result.IsFailure)
                return result;
            return ForbiddenModule(MatchModuleName, true);
        }

        public Result LoadMatchModule(RoiPosEnum roiPos)
        {
            Result result = null;

            if (CurrentModule == null || !CurrentModule.Name.Contains(MatchModuleName))
            {
                result = LoadVmModule(MatchModuleName);
                if (result.IsFailure)
                    return result;
            }

            var tool = CurrentModule as IMVSFastFeatureMatchModuTool;
            if (tool == null)
                return Result.Fail("快速匹配模块类型转换失败");

            
            if(roiPos != RoiPosEnum.None)
            {
                if (roiPos == RoiPosEnum.Full)
                {
                    tool.ModuParams.MinScore = _dataModel.AoiMatchScore;
                    tool.ModuParams.MaxMatchNum = 50;
                    tool.ModuParams.NumLimitEnable = false;
                    tool.ModuParams.OKWhenNumIsZero = true;
                }
                else
                {
                    tool.ModuParams.MinScore = _dataModel.MatchScore;
                    tool.ModuParams.MaxMatchNum = 1;
                    tool.ModuParams.NumLimitEnable = true;
                    tool.ModuParams.OKWhenNumIsZero = false;
                }
                ApplyCameraRoi(tool, roiPos);

            }
            return ForbiddenModule(MatchModuleName, false);
        }

        public Result SetVmModuleParam(Window owner = null)
        {
            if (CurrentModule == null)
                return Result.Fail("请先选择待设置的模块");

            CurrentProcess.ContinuousRunEnable = false;
            _moduleParamDialogService.Show(CurrentModule);
            CurrentProcess.ContinuousRunEnable = true;
            return Save();
        }

        public Result Save()
        {
            try
            {
                VmSolution.Save();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Fail($"保存失败--- {ex.Message}");
            }
        }

        public void SingleRun()
        {
            DiscontinueRun();
            CurrentProcess?.Run();
        }

        public void ContinueRun()
        {
            if (CurrentProcess != null)
                CurrentProcess.ContinuousRunEnable = true;
        }

        public void DiscontinueRun()
        {
            if (CurrentProcess != null)
                CurrentProcess.ContinuousRunEnable = false;
        }

        public bool IsContinuousRun()
        {
            return  CurrentProcess != null && CurrentProcess.ContinuousRunEnable;
        }

        private Result LoadVmProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return Result.Fail("请先选择待加载流程");

            ModuleNameList.Clear();
            CurrentProcess = (VmProcedure)VmSolution.Instance[processName];
            if (CurrentProcess == null)
                return Result.Fail("流程加载失败");

            CurrentModule = null;
            CurrentProcess.SetContinousRunInterval(DefaultContinuousRunIntervalMs);

            foreach (var module in CurrentProcess.Modules)
                ModuleNameList.Add(module.Name);

            return Result.Success();
        }

        private Result LoadVmModule(string moduleName)
        {
            if (_renderHost == null)
                return Result.Fail("加载失败--- VmRenderControl 未绑定，无法显示实时画面");
            if (CurrentProcess == null)
                return Result.Fail("加载失败--- 请先绑定流程");

            var result = FindModule(moduleName);
            if (result.IsFailure)
                return result;

            CurrentModule = result.Data;
            _renderHost.ModuleSource = CurrentModule;
            ContinueRun();
            return Result.Success();
        }

        private Result ForbiddenModule(string moduleName, bool isForbidden)
        {
            var module = CurrentProcess.Modules.FirstOrDefault(o => o.Name.Contains(moduleName));
            if (module is null)
                return Result.Fail($"未找到指定模块--- {moduleName}");

            module.IsForbidden = isForbidden;
            return Result.Success();
        }

        private void ApplyCameraRoi(IMVSFastFeatureMatchModuTool tool, RoiPosEnum roiPos)
        {
            var cameraSize = _dataModel.CameraSize;
            RectBox rectBox = null;

            switch (roiPos)
            {
                case RoiPosEnum.Center:
                    rectBox = new RectBox(new VM.PlatformSDKCS.PointF(cameraSize.X / 2f, cameraSize.Y / 2f), cameraSize.X / 2f, cameraSize.Y / 2f, 0);
                    break;
                case RoiPosEnum.LeftTop:
                    rectBox = new RectBox(new VM.PlatformSDKCS.PointF(cameraSize.X / 4f, cameraSize.Y / 4f), cameraSize.X / 2f, cameraSize.Y / 2f, 0);
                    break;
                case RoiPosEnum.RightTop:
                    rectBox = new RectBox(new VM.PlatformSDKCS.PointF(cameraSize.X * 3f / 4f, cameraSize.Y / 4f), cameraSize.X / 2f, cameraSize.Y / 2f, 0);
                    break;
                case RoiPosEnum.LeftBottom:
                    rectBox = new RectBox(new VM.PlatformSDKCS.PointF(cameraSize.X / 4f, cameraSize.Y * 3f / 4f), cameraSize.X / 2f, cameraSize.Y / 2f, 0);
                    break;
                case RoiPosEnum.RightBottom:
                    rectBox = new RectBox(new VM.PlatformSDKCS.PointF(cameraSize.X * 3f / 4f, cameraSize.Y * 3f / 4f), cameraSize.X / 2f, cameraSize.Y / 2f, 0);
                    break;
                case RoiPosEnum.Full:
                    rectBox = new RectBox(new VM.PlatformSDKCS.PointF(cameraSize.X / 2f, cameraSize.Y / 2f), cameraSize.X, cameraSize.Y, 0);
                    break;
                
            }
            if(rectBox == null)
                return;
            var roiManagerField = typeof(FastFeatureMatchRoiManager).GetField("moduRoiManager", BindingFlags.Instance | BindingFlags.NonPublic);
            var roiManager = roiManagerField?.GetValue(tool.ModuParams.ModuRoiManager) as RoiManager;
            if (roiManager != null)
                roiManager.SetDrawRoi(rectBox, (uint)cameraSize.X, (uint)cameraSize.Y);

            tool.ModuParams.ModuRoiManager.RoiRectangle = rectBox;
        }
    }
}
