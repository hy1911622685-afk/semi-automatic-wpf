using MyAsset.Wpf.Infrastructure;
using HKVision.Wpf.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HKVision.Wpf
{
    public partial class HikVisionHelper
    {
        private const int DieSizeStableSampleCount = 5;// 稳定性采样次数
        private const int WaitTime = 20;// 每次采样的等待时间，单位毫秒
        private const int DieSizeMaxSearchSteps = 20; //同向搜索最大步数，防止死循环

        public event Action<List<Point2D>, double, double, double, double> OnFovScanned;

        private CancellationTokenSource _ctsScan;

        public Func<(double X, double Y)> ReadAxisPosFunc;
        public Func<double, double, Task<short>> MoveAbsoluteAsync;
        public Func<double, double, Task<short>> MoveSync;
        public Func<VisionMoveDir, double, Task<short>> OnAxisMove;
        public Func<double> ReadToleranceFactorFunc;

        public async Task StartScanningAsync()
        {
            if (_ctsScan != null)
            {
                if (!_ctsScan.IsCancellationRequested)
                    return;

                _ctsScan.Dispose();
            }
            VmManager.LoadMatchModule(RoiPosEnum.Full);

            _ctsScan = new CancellationTokenSource();

            try
            {
                OnLogMessage("开始全局视觉扫描");
                var scanResult = await ScanLoopSmartBoundaryAsync(_ctsScan.Token);
                LogScanLoopResult(scanResult);
            }
            finally
            {
                VmManager.LoadDefaultModule();
                _ctsScan?.Dispose();
                _ctsScan = null;
            }
        }

        public void StopScanning()
        {
            var cts = _ctsScan;
            if (cts == null || cts.IsCancellationRequested)
                return;

            cts.Cancel();
            DataModel.OnLogMessage("扫描流程已被中止");
        }

        private async Task<VisionScanLoopResult> ScanLoopSmartBoundaryAsync(CancellationToken token)
        {
            int consecutiveEmptyDownSteps = 0;

            while (!token.IsCancellationRequested)
            {
                var rowAnchorPos = ReadAxisPosFunc?.Invoke();
                if (rowAnchorPos == null)
                {
                    DataModel.OnLogMessage("无法读取当前轴坐标，扫描终止。");
                    return VisionScanLoopResult.Failed;
                }

                int diesFound = await ProcessVisionAndPushToUiAsync();
                if (diesFound < 1)
                {
                    consecutiveEmptyDownSteps++;
                    if (consecutiveEmptyDownSteps >= DataModel.AllowableErrorNumber)
                    {
                        DataModel.OnLogMessage("连续多行无数据，判定全盘扫描结束。");
                        return VisionScanLoopResult.Completed;
                    }
                }
                else
                {
                    consecutiveEmptyDownSteps = 0;
                }

                if (!await ScanOneDirectionAsync(VisionMoveDir.Right, token))
                    return token.IsCancellationRequested ? VisionScanLoopResult.Cancelled : VisionScanLoopResult.Failed;

                if (token.IsCancellationRequested)
                    return VisionScanLoopResult.Cancelled;
                VmManager.ContinueRun();
                if (!await TryMoveAbsoluteAsync(rowAnchorPos.Value.X, rowAnchorPos.Value.Y))
                    return token.IsCancellationRequested ? VisionScanLoopResult.Cancelled : VisionScanLoopResult.Failed;
                if (token.WaitHandle.WaitOne(WaitTime))
                    return VisionScanLoopResult.Cancelled;

                if (!await ScanOneDirectionAsync(VisionMoveDir.Left, token))
                    return token.IsCancellationRequested ? VisionScanLoopResult.Cancelled : VisionScanLoopResult.Failed;

                if (token.IsCancellationRequested)
                    return VisionScanLoopResult.Cancelled;
                VmManager.ContinueRun();
                if (!await TryMoveAbsoluteAsync(rowAnchorPos.Value.X, rowAnchorPos.Value.Y))
                    return token.IsCancellationRequested ? VisionScanLoopResult.Cancelled : VisionScanLoopResult.Failed;

                if (token.WaitHandle.WaitOne(WaitTime))
                    return VisionScanLoopResult.Cancelled;

                if (!await TryMoveAxisAsync(VisionMoveDir.Front, DataModel.AOIScanStepY))
                    return token.IsCancellationRequested ? VisionScanLoopResult.Cancelled : VisionScanLoopResult.Failed;
                VmManager.DiscontinueRun();
                if (token.WaitHandle.WaitOne(WaitTime))
                    return VisionScanLoopResult.Cancelled;
            }

            return token.IsCancellationRequested ? VisionScanLoopResult.Cancelled : VisionScanLoopResult.Completed;
        }

        private void LogScanLoopResult(VisionScanLoopResult scanResult)
        {
            switch (scanResult)
            {
                case VisionScanLoopResult.Completed:
                    DataModel.OnLogMessage("扫描流程正常结束");
                    break;
                case VisionScanLoopResult.Cancelled:
                    DataModel.OnLogMessage("扫描流程已取消");
                    break;
                case VisionScanLoopResult.Failed:
                    DataModel.OnLogMessage("扫描流程因错误终止");
                    break;
            }
        }

        private async Task<bool> ScanOneDirectionAsync(VisionMoveDir dir, CancellationToken token)
        {
            int emptyCount = 0;

            while (emptyCount < DataModel.AllowableErrorNumber && !token.IsCancellationRequested)
            {
                if (!await TryMoveAxisAsync(dir, DataModel.AOIScanStepX))
                    return false;
                if (token.WaitHandle.WaitOne(WaitTime))
                    break;

                int diesFound = await ProcessVisionAndPushToUiAsync();
                if (diesFound == 0)
                    emptyCount++;
                else
                    emptyCount = 0;
            }

            return true;
        }

        

       

        private async Task<bool> TryMoveAbsoluteAsync(double x, double y)
        {
            if (MoveAbsoluteAsync == null)
            {
                DataModel.OnLogMessage("绝对移动接口未绑定，扫描终止。");
                return false;
            }

            short ret = await MoveAbsoluteAsync(x, y);
            if (ret == 0)
                return true;

            DataModel.OnLogMessage($"移动到行锚点失败，X:{x:F4}, Y:{y:F4}, 错误码:{ret}");
            return false;
        }

        private async Task<bool> TryMoveAxisAsync(VisionMoveDir dir, double distance)
        {
            if (OnAxisMove == null)
            {
                DataModel.OnLogMessage("轴移动接口未绑定，扫描终止。");
                return false;
            }

            short ret = await OnAxisMove(dir, distance);
            if (ret == 0)
                return true;

            DataModel.OnLogMessage($"{dir}方向移动失败，距离:{distance:F4}, 错误码:{ret}");
            return false;
        }

        private async Task<int> ProcessVisionAndPushToUiAsync()
        {
            var realPos = ReadAxisPosFunc?.Invoke();
            if (realPos == null)
                return 0;

            DataModel.Transformer.Tx = realPos.Value.X;
            DataModel.Transformer.Ty = realPos.Value.Y;

            List<Point2D> rawPixelDies = await ReadServerAsync();
            VmManager.DiscontinueRun();

            if (rawPixelDies == null || rawPixelDies.Count == 0)
                return 0;

            List<Point2D> physicalDies = DataModel.Transformer.TransformList(rawPixelDies);
            OnFovScanned?.Invoke(
                physicalDies,
                realPos.Value.X - DataModel.AOIScanStepX / 2,
                realPos.Value.X + DataModel.AOIScanStepX / 2,
                realPos.Value.Y - DataModel.AOIScanStepY / 2,
                realPos.Value.Y + DataModel.AOIScanStepY / 2);

            return physicalDies.Count;
        }



        public async Task<double> StartScanningDieDimensionAsync(VisionMoveDir moveDir)
        {
            if (_ctsScan != null)
            {
                if (!_ctsScan.IsCancellationRequested)
                    return 0;

                _ctsScan.Dispose();
            }

            _ctsScan = new CancellationTokenSource();
            var token = _ctsScan.Token;

            try
            {
                VmManager.LoadMatchModule(RoiPosEnum.Center);

                OnLogMessage($"开始扫描晶圆{GetDieDimensionName(moveDir)}");
                double dieDimension = await ScanDieDimensionAsync(moveDir, token);
                if (dieDimension > 0)
                    OnLogMessage($"Die{GetDieDimensionName(moveDir)}扫描完成：{dieDimension:F4}");
                else if (token.IsCancellationRequested)
                    OnLogMessage($"Die{GetDieDimensionName(moveDir)}扫描已取消");
                else
                    OnLogMessage($"Die{GetDieDimensionName(moveDir)}扫描失败，未找到第二个模板");

                return dieDimension;
            }
            finally
            {
                VmManager.LoadDefaultModule();
                _ctsScan?.Dispose();
                _ctsScan = null;
            }
        }




        private async Task<double> ScanDieDimensionAsync(VisionMoveDir moveDir, CancellationToken token)
        {
            double toleranceFactor = ReadWaferToleranceFactor();
            var firstDie = await CaptureStableDieCenterAsync(toleranceFactor, token);
            if (firstDie == null)
                return 0;

            double searchStep = DataModel.DieDimensionScanStep;
            if (searchStep <= 0 || OnAxisMove == null)
                return 0;

            for (int i = 0; i < DieSizeMaxSearchSteps && !token.IsCancellationRequested; i++)
            {
                short moveResult = await OnAxisMove(moveDir, searchStep);
                if (moveResult != 0)
                {
                    DataModel.OnLogMessage($"扫描Die尺寸时{moveDir}方向移动失败");
                    return 0;
                }

                if (token.WaitHandle.WaitOne(WaitTime))
                    break;

                var currentCenter = await ReadCurrentCenterPhysicalPointAsync(token);
                if (currentCenter == null)
                    continue;

                if (IsSameDie(firstDie.Value.Center, currentCenter.Value, firstDie.Value.ToleranceX, firstDie.Value.ToleranceY))
                    continue;

                return GetDieDimension(firstDie.Value.Center, currentCenter.Value, moveDir);
            }

            return 0;
        }

        private static double GetDieDimension(Point2D firstDieCenter, Point2D secondDieCenter, VisionMoveDir moveDir)
        {
            return moveDir == VisionMoveDir.Front || moveDir == VisionMoveDir.Back
                ? Math.Abs(secondDieCenter.Y - firstDieCenter.Y)
                : Math.Abs(secondDieCenter.X - firstDieCenter.X);
        }

        private static string GetDieDimensionName(VisionMoveDir moveDir)
        {
            return moveDir == VisionMoveDir.Front || moveDir == VisionMoveDir.Back ? "高度" : "宽度";
        }

        private async Task<(Point2D Center, double ToleranceX, double ToleranceY)?> CaptureStableDieCenterAsync(
            double toleranceFactor,
            CancellationToken token)
        {
            var samples = new List<Point2D>(DieSizeStableSampleCount);

            for (int i = 0; i < DieSizeStableSampleCount && !token.IsCancellationRequested; i++)
            {
                var centerPoint = await ReadCurrentCenterPhysicalPointAsync(token);
                if (centerPoint != null)
                    samples.Add(centerPoint.Value);

                if (samples.Count < DieSizeStableSampleCount && token.WaitHandle.WaitOne(WaitTime))
                    break;
            }

            if (samples.Count < DieSizeStableSampleCount)
            {
                DataModel.OnLogMessage("扫描Die尺寸时无法稳定获取第一个模板中心点");
                return null;
            }

            double centerX = 0;
            double centerY = 0;
            foreach (var sample in samples)
            {
                centerX += sample.X;
                centerY += sample.Y;
            }

            centerX /= samples.Count;
            centerY /= samples.Count;
            var center = new Point2D(centerX, centerY);

            double maxOffsetX = 0;
            double maxOffsetY = 0;
            foreach (var sample in samples)
            {
                maxOffsetX = Math.Max(maxOffsetX, Math.Abs(sample.X - center.X));
                maxOffsetY = Math.Max(maxOffsetY, Math.Abs(sample.Y - center.Y));
            }

            double toleranceX = maxOffsetX + maxOffsetX * toleranceFactor;
            double toleranceY = maxOffsetY + maxOffsetY * toleranceFactor;
            return (center, toleranceX, toleranceY);
        }

        private async Task<Point2D?> ReadCurrentCenterPhysicalPointAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;

            var realPos = ReadAxisPosFunc?.Invoke();
            if (realPos == null)
                return null;

            DataModel.Transformer.Tx = realPos.Value.X;
            DataModel.Transformer.Ty = realPos.Value.Y;

            var pixelCenterPoint = await ReadServerCenterDataAsync();
            if (pixelCenterPoint == null)
                return null;

            return DataModel.Transformer.Transform(pixelCenterPoint.Value);
        }

        private static bool IsSameDie(Point2D firstDieCenter, Point2D currentCenter, double toleranceX, double toleranceY)
        {
            return Math.Abs(currentCenter.X - firstDieCenter.X) <= toleranceX &&
                   Math.Abs(currentCenter.Y - firstDieCenter.Y) <= toleranceY;
        }

        private double ReadWaferToleranceFactor()
        {
            double toleranceFactor = ReadToleranceFactorFunc?.Invoke() ?? 0.3d;
            return toleranceFactor > 0 && toleranceFactor < 1 ? toleranceFactor : 0.3d;
        }


    }

    internal enum VisionScanLoopResult
    {
        Completed,
        Cancelled,
        Failed
    }
}
