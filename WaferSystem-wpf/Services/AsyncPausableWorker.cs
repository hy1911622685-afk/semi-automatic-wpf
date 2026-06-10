using HKVision.Wpf;
using MotionCard;
using MyAsset.Wpf.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;
using WaferMap.Wpf.Model;
using WaferMap.Wpf.ViewModels;

namespace WaferSystem.Wpf.Services
{
    public class AsyncPausableWorker : IDisposable
    {
        private enum RunExitReason
        {
            None,
            Completed,
            UserStopped,
            InternalStopped
        }

        private readonly WaferMainViewModel _waferViewModel;
        private readonly HikVisionHelper _hikVisionHelper;
        private readonly SemaphoreSlim _pauseSemaphore = new SemaphoreSlim(0, 1);
        private CancellationTokenSource _cts;
        private Task _workerTask;
        private volatile bool _isPauseWaiting;
        private RunExitReason _runExitReason;

        public Action WorkStarted { get; set; }
        public Action WorkStopped { get; set; }

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        public AsyncPausableWorker(WaferMainViewModel waferViewModel, HikVisionHelper hikVisionHelper)
        {
            _waferViewModel = waferViewModel;
            _hikVisionHelper = hikVisionHelper;
        }

        public void StartAsync()
        {
            if (IsRunning || (_workerTask != null && !_workerTask.IsCompleted))
                return;

            OnLogMessage("正在启动工作器...");
            DisposeCancellationTokenSource();
            ResetPauseSignal();

            _cts = new CancellationTokenSource();
            _cts.CancelAfter(TimeSpan.FromMinutes(5000));
            _runExitReason = RunExitReason.None;
            IsRunning = true;
            IsPaused = false;
            _isPauseWaiting = false;

            WorkStarted?.Invoke();
            OnLogMessage("工作器开始运行");
            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }

        public void Pause()
        {
            if (!IsRunning || IsPaused)
                return;

            IsPaused = true;
            OnLogMessage("工作器已暂停");
        }

        public void Resume()
        {
            if (!IsRunning || !IsPaused)
                return;

            IsPaused = false;
            ReleasePauseWaiter();
            OnLogMessage("工作器已恢复");
        }

        public async Task StopAsync()
        {
            if (!IsRunning && (_workerTask == null || _workerTask.IsCompleted))
                return;

            _runExitReason = RunExitReason.UserStopped;
            OnLogMessage("正在停止工作器...");
            RequestStop();
            await WaitForWorkerToFinishAsync();
            OnLogMessage("工作器已停止");
        }

        private async Task WorkerLoopAsync(CancellationToken token)
        {
            int iteration = 0;

            try
            {
                while (IsRunning && !token.IsCancellationRequested)
                {
                    await WaitIfPausedAsync(token);
                    if (!IsRunning || token.IsCancellationRequested)
                        break;

                    iteration++;
                    bool shouldContinue = await ExecuteWorkIterationAsync(iteration, token);
                    if (!shouldContinue)
                    {
                        IsRunning = false;
                        IsPaused = false;
                        break;
                    }

                    await Task.Delay(50, token);
                }
            }
            catch (OperationCanceledException)
            {
                OnLogMessage("工作循环被取消");
            }
            catch (Exception ex)
            {
                MarkInternalStopped();
                OnLogMessage($"工作循环发生错误: {ex.Message}");
            }
            finally
            {
                FinishRun();
            }
        }

        private async Task<bool> ExecuteWorkIterationAsync(int iteration, CancellationToken token)
        {
            try
            {
                var point = await _hikVisionHelper.ReadServerCenterDataAsync();
                if (point is null)
                {
                    OnLogMessage("未获取到识别点");
                    MarkInternalStopped();
                    return false;
                }

                bool dieResult = await ExecuteDieAsync(iteration, token);
                if (!dieResult)
                    return false;

                var offset = _waferViewModel.PlotHelper.MyNavigator.NextDiePos(out var die);
                if (offset is null)
                {
                    MarkCompleted();
                    _waferViewModel.DataModel.RefreshPlot();
                    OnLogMessage($"测试完成，一共测试了 {iteration} 个晶粒");
                    return false;
                }

                await MyLTDMC.MoveSync(offset.Value.X, offset.Value.Y);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                OnLogMessage($"第 {iteration} 个晶粒的测试执行失败：{ex.Message}");
                return true;
            }
        }

        private async Task<bool> ExecuteDieAsync(int iteration, CancellationToken token)
        {
            OnLogMessage($"开始第 {iteration} 个晶粒的测试");

            for (int i = 0; i <= 100; i += 10)
            {
                if (!IsRunning || token.IsCancellationRequested)
                    return false;

                OnLogMessage($"进度：{i}% - 处理数据 {iteration}.{i / 10}");
                await Task.Delay(20, token);
            }

            var currentDie = _waferViewModel.PlotHelper.DataModel.SelectedDie;
            if (currentDie != null)
            {
                _waferViewModel.ApplyBinToDie(currentDie, iteration % 5 != 0
                    ? ColorManager.PassedBinCommand
                    : ColorManager.FailedBinCommand);
            }

            OnLogMessage($"完成第 {iteration} 个晶粒的测试");
            return true;
        }

        private async Task WaitIfPausedAsync(CancellationToken token)
        {
            if (!IsPaused)
                return;

            OnLogMessage("工作器已暂停，等待继续信号。");
            _isPauseWaiting = true;
            try
            {
                await _pauseSemaphore.WaitAsync(token);
            }
            finally
            {
                _isPauseWaiting = false;
            }

            if (!token.IsCancellationRequested)
                OnLogMessage("工作器已恢复执行。");
        }

        private void RequestStop()
        {
            IsRunning = false;
            IsPaused = false;
            _cts?.Cancel();
            ReleasePauseWaiter();
        }

        private async Task WaitForWorkerToFinishAsync()
        {
            Task workerTask = _workerTask;
            if (workerTask == null)
                return;

            try
            {
                await workerTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                OnLogMessage($"停止时发生异常: {ex.Message}");
            }
        }

        private void FinishRun()
        {
            IsRunning = false;
            IsPaused = false;
            _isPauseWaiting = false;

            if (_runExitReason == RunExitReason.Completed)
                OnLogMessage("测试已经全部完成");
            else if (_runExitReason == RunExitReason.InternalStopped)
                OnLogMessage("工作器已停止");

            WorkStopped?.Invoke();
            DisposeCancellationTokenSource();
            _workerTask = null;
            _runExitReason = RunExitReason.None;
        }

        private void ReleasePauseWaiter()
        {
            if (!_isPauseWaiting)
                return;

            try
            {
                _pauseSemaphore.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }

        private void ResetPauseSignal()
        {
            while (_pauseSemaphore.CurrentCount > 0)
            {
                _pauseSemaphore.Wait();
            }
        }

        private void DisposeCancellationTokenSource()
        {
            if (_cts == null)
                return;

            _cts.Dispose();
            _cts = null;
        }

        private void MarkCompleted()
        {
            if (_runExitReason == RunExitReason.None)
                _runExitReason = RunExitReason.Completed;
        }

        private void MarkInternalStopped()
        {
            if (_runExitReason == RunExitReason.None)
                _runExitReason = RunExitReason.InternalStopped;
        }

        private void OnLogMessage(string message)
        {
            RuntimeLogMessenger.Broadcast("WaferSystem-wpf", "工作流", message);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            DisposeCancellationTokenSource();
            _pauseSemaphore.Dispose();
        }
    }
}
