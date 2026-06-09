using Communication.Wpf;
using MotionCard;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using WaferMap.Wpf.Model;
using WaferMap.Wpf.ViewModels;

namespace WaferSystem.Wpf.Services
{
    public sealed class SocketCommandService
    {
        private WaferMainViewModel _waferViewModel;

        public SocketCommandService(WaferMainViewModel waferViewModel)
        {
            _waferViewModel = waferViewModel;
        }

        public void BindCommands(AsyncCommandExecutor executor)
        {
            if (executor == null)
                return;

            executor.MoveCenterFunc += CmdCenter;
            executor.Get_seq_DiesFunc += CmdGetSeqDies;
            executor.MoveFrontFunc += CmdFront;
            executor.MoveStopFunc += CmdStop;
            executor.ChuckContactFunc += CmdContact;
            executor.ChuckSeparationFunc += CmdSeparation;
            executor.MoveChuckHomeFunc += CmdMoveChuckHome;
            executor.MoveFirstDieFunc += CmdMoveFirstDie;
            executor.MoveNextDieFunc += CmdMoveNextDie;
            executor.MoveDieFunc += CmdMoveDie;
            executor.GetSelectDiesNumFunc += CmdSelectDiesNum;
            executor.GetCurrentDiePosFunc += CmdCurrentDiePos;
            executor.SetCurrentDieBinFunc += CmdCurrentDieBin;
            executor.UnloadWaferFunc += CmdUnloadWafer;
        }

        private async Task<string[]> CmdGetSeqDies(string param)
        {
            return await RunOnUiAsync(() =>
            {
                var plotHelper = _PlotHelper;
                if (plotHelper?.DataModel == null)
                    return new[] { "False", "-1001" };

                var dies = plotHelper.MyNavigator.TestQueueDiesInMoveOrder.ToList();

                if (dies.Count == 0)
                    return new[] { "False", "-1001" };

                var builder = new StringBuilder(dies.Count * 12);
                for (int i = 0; i < dies.Count; i++)
                {
                    if (i > 0)
                        builder.Append(';');

                    builder
                        .Append(dies[i].Index)
                        .Append(',')
                        .Append(dies[i].GridX)
                        .Append(',')
                        .Append(dies[i].GridY);
                }

                return new[] { "OK", "0", builder.ToString() };
            });
        }

        private static async Task<string[]> CmdCenter(string param)
        {
            short res = await MyLTDMC.InitCenter();
            return Result(res);
        }

        private static async Task<string[]> CmdFront(string param)
        {
            short res = await MyLTDMC.InitFront();
            return Result(res);
        }

        private static async Task<string[]> CmdStop(string param)
        {
            short res = await MyLTDMC.dmc_emg_stop();
            return Result(res);
        }

        private static async Task<string[]> CmdContact(string param)
        {
            short res = await MyLTDMC.AxisZMove(ZAxisHeightEnum.Contact);
            return BoolResult(res);
        }

        private static async Task<string[]> CmdSeparation(string param)
        {
            short res = await MyLTDMC.AxisZMove(ZAxisHeightEnum.Separation);
            return BoolResult(res);
        }

        private async Task<string[]> CmdMoveFirstDie(string param)
        {
            var target = await RunOnUiAsync(() =>
            {
                var plotHelper = _PlotHelper;
                if (plotHelper?.DataModel == null || !plotHelper.DataModel.IsSync)
                    return (Position: ((double X, double Y)?)null, Die: (DieModel)null, Error: "-1001");

                var position = plotHelper.MyNavigator.FirstDiePos(out DieModel firstDie);
                return position is null || firstDie is null
                    ? (Position: ((double X, double Y)?)null, Die: (DieModel)null, Error: "-1002")
                    : (Position: position, Die: firstDie, Error: (string)null);
            });

            if (target.Position is null || target.Die is null)
                return new[] { "False", "-1002" };

            short res = await MyLTDMC.AbsMoveSync(target.Position.Value.X, target.Position.Value.Y);
            if (res == 0)
            {
                await RunOnUiAsync(() =>
                {
                    _PlotHelper?.MyRenderer.UpdateSelectedDieDisplay(target.Die);
                    return true;
                });

                return new[] { "OK", Format(res), Format(target.Die.Index), Format(target.Die.GridX), Format(target.Die.GridY) };
            }
            else
            {
                return new[] { "False", Format(res) };
            }
        }

        private async Task<string[]> CmdMoveNextDie(string param)
        {
            var target = await RunOnUiAsync(() =>
            {
                var plotHelper = _PlotHelper;
                if (plotHelper?.DataModel?.SelectedDie is null)
                    return (Offset: ((double X, double Y)?)null, Die: (DieModel)null, GridX: 0, GridY: 0, Error: "-1001");

                int gridX = plotHelper.DataModel.SelectedDie.GridX;
                int gridY = plotHelper.DataModel.SelectedDie.GridY;
                var offset = plotHelper.MyNavigator.NextDiePos(out var die);

                return offset is null || die is null
                    ? (Offset: ((double X, double Y)?)null, Die: (DieModel)null, GridX: gridX, GridY: gridY, Error: "-1002")
                    : (Offset: offset, Die: die, GridX: gridX, GridY: gridY, Error: (string)null);
            });

            if (target.Offset is null || target.Die is null)
                return new[] { "False", target.Error ?? "-1002" };

            short res = await MyLTDMC.MoveSync(target.Offset.Value.X, target.Offset.Value.Y);
            if (res == 0)
            {
                await RunOnUiAsync(() =>
                {
                    _PlotHelper?.MyRenderer.UpdateSelectedDieDisplay(target.Die);
                    return true;
                });

                return new[] { "OK", Format(res), Format(target.Die.Index), Format(target.GridX), Format(target.GridY) };
            }
            else
            {
                return new[] { "False", Format(res) };
            }
        }

        private async Task<string[]> CmdMoveDie(string param)
        {
            string[] parts = (param ?? string.Empty).Split(',');
            if (parts.Length != 2 ||
                !double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                return new[] { "False", "-1001" };
            }
            var target = await RunOnUiAsync(() =>
            {
                var plotHelper = _PlotHelper;
                var die = plotHelper?.MyNavigator.TestQueueDiesInMoveOrder.FirstOrDefault(o => o.GridX == x && o.GridY == y);
                var offset = die == null
                    ? null
                    : plotHelper.MyNavigator.CalculateAxisMoveOffset(die);

                return (Die: die, Offset: offset);
            });

            if (target.Die == null || target.Offset == null)
                return new[] { "False", "-1002" };

            short res = await MyLTDMC.MoveSync(target.Offset.Value.X, target.Offset.Value.Y);
            if (res == 0)
            {
                await RunOnUiAsync(() =>
                {
                    _PlotHelper?.MyRenderer.UpdateSelectedDieDisplay(target.Die);
                    return true;
                });

                return new[] { "OK", Format(res) };
            }
            else
            {
                return new[] { "False", Format(res) };
            }

        }

        private async Task<string[]> CmdSelectDiesNum(string param)
        {
            int? count = await RunOnUiAsync(() =>
            {
                var plotHelper = _PlotHelper;
                return plotHelper?.DataModel == null
                    ? (int?)null
                    : plotHelper.MyNavigator.TestQueueDiesInMoveOrder.Count;
            });

            return count.HasValue
                ? new[] { "OK", "0", Format(count.Value) }
                : new[] { "False", "-1001" };
        }

        private async Task<string[]> CmdCurrentDiePos(string param)
        {
            var current = await RunOnUiAsync(() =>
            {
                var currentDie = _PlotHelper?.DataModel?.SelectedDie;
                return currentDie is null
                    ? null
                    : new[] { "OK", "0", Format(currentDie.Index), Format(currentDie.GridX), Format(currentDie.GridY) };
            });

            return current ?? new[] { "False", "-1001" };
        }

        private async Task<string[]> CmdCurrentDieBin(string param)
        {
            return await RunOnUiAsync(() =>
            {
                var plotHelper = _waferViewModel?.PlotHelper;
                if (plotHelper == null || !plotHelper.DataModel.ColorManager.TryResolveBinCommand(param, out string binCommand))
                    return new[] { "False", "-1001" };

                var currentDie = plotHelper.DataModel.SelectedDie;
                if (currentDie == null)
                    return new[] { "False", "-1002" };

                currentDie.ApplyBin(binCommand);
                plotHelper.DataModel.UpdateDieState(currentDie);
                return new[] { "OK", "0" };
            });
        }

        private static async Task<string[]> CmdUnloadWafer(string param)
        {
            short res = await MyLTDMC.InitFront();
            return BoolResult(res);
        }

        private async Task<string[]> CmdMoveChuckHome(string param)
        {
            if (_PlotHelper?.DataModel == null)
                return new[] { "False", "-1001" };

            var home = await RunOnUiAsync(() => _PlotHelper.DataModel.PhysicalReferencePosition);
            short res = await MyLTDMC.AbsMoveSync(home.X, home.Y);
            return BoolResult(res);
        }

        private PlotHelper _PlotHelper => _waferViewModel?.PlotHelper;

        private static string[] Result(short res) => new[] { Format(res) };

        private static string[] BoolResult(short res) =>
            res == 0 ? new[] { "OK", Format(res) } : new[] { "False", Format(res) };

        private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static T RunOnUi<T>(Func<T> action)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return action();

            return dispatcher.Invoke(action);
        }

        private static Task<T> RunOnUiAsync<T>(Func<T> action)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return Task.FromResult(action());

            return dispatcher.InvokeAsync(action, DispatcherPriority.Background).Task;
        }
    }
}
