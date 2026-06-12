using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HKVision.Wpf;
using HKVision.Wpf.Views;
using MotionCard;
using MotionCard.Wpf.ViewModels;
using MotionCard.Wpf.Views;
using MyAsset.Wpf;
using MyAsset.Wpf.Views;
using MyAsset.Wpf.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Communication.Wpf;
using WaferMap.Wpf.ViewModels;
using WaferMap.Wpf.Views;
using WaferSystem.Wpf.Models;
using WaferSystem.Wpf.Services;
using WaferSystem.Wpf.Views;

namespace WaferSystem.Wpf.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly HikVisionHelper _hikVisionHelper;
        private readonly WaferMainViewModel _waferViewModel;
        private readonly MotionMainViewModel _motionViewModel;
        private readonly MotionService _motionService;
        private readonly VisionService _visionService;
        private readonly SocketCommandService _socketCommandService;
        private readonly AsyncPausableWorker _worker;
        private readonly TabItem _workflowTab;
        private readonly TabItem _motionTab;
        private Action _socketStopListeningAction;
        private SystemPage _waferPage;
        private SystemPage _visionPage;
        private WaferMainView _waferWorkbenchView;
        private VisionMainView _visionWorkbenchView;
        private bool _isDisposing;
        private bool _startupInitialized;

        [ObservableProperty]
        private SystemPage selectedPage;

        [ObservableProperty]
        private string statusText = "就绪";

        [ObservableProperty]
        private bool isStartupLoading = true;

        [ObservableProperty]
        private string startupLoadingMessage = "正在准备启动...";

        [ObservableProperty]
        private bool isWorkflowRunning;

        [ObservableProperty]
        private bool isWorkflowPaused;

        [ObservableProperty]
        private bool isWorkflowInteractionLocked;

        [ObservableProperty]
        private UserControl generalWorkbenchView;

        [ObservableProperty]
        private bool isWaferWorkbenchMain = true;

        [ObservableProperty]
        private bool isVisionWorkbenchMain;

        [ObservableProperty]
        private bool isDualWorkbenchVisible = true;

        [ObservableProperty]
        private Visibility workbenchSwitcherVisibility = Visibility.Visible;

        public ObservableCollection<SystemPage> Pages { get; } = new ObservableCollection<SystemPage>();
        public ObservableCollection<TabItem> RightTabs { get; } = new ObservableCollection<TabItem>();
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public WaferMainViewModel WaferViewModel => _waferViewModel;
        public MotionMainViewModel MotionViewModel => _motionViewModel;
        public UserControl WaferWorkbenchView => GetWaferWorkbenchView();
        public UserControl VisionWorkbenchView => GetVisionWorkbenchView();
        public bool IsWaferWorkbenchVisible => IsDualWorkbenchVisible;
        public bool IsVisionWorkbenchVisible => IsDualWorkbenchVisible;
        public bool IsGeneralWorkbenchVisible => !IsDualWorkbenchVisible;
        public bool IsWaferWorkbenchMini => IsDualWorkbenchVisible && !IsWaferWorkbenchMain;
        public bool IsVisionWorkbenchMini => IsDualWorkbenchVisible && !IsVisionWorkbenchMain;
        public int WaferViewZIndex => IsWaferWorkbenchMini ? 10 : 0;
        public int VisionViewZIndex => IsVisionWorkbenchMini ? 20 : 0;
        public Action<AsyncCommandExecutor> SocketCommandBinder => _socketCommandService.BindCommands;
        public Action SocketDeviceConnectedAction => HandleSocketDeviceConnected;
        public Action SocketDeviceDisconnectedAction => HandleSocketDeviceDisconnected;
        public Action<Action> SocketStopListeningBinder => BindSocketStopListening;

        public MainWindowViewModel()
        {
            WeakReferenceMessenger.Default.Register<RuntimeLogMessage>(this, static (recipient, message) =>
            {
                ((MainWindowViewModel)recipient).HandleRuntimeLog(message);
            });

            _hikVisionHelper = new HikVisionHelper();
            _waferViewModel = new WaferMainViewModel();
            _motionViewModel = new MotionMainViewModel();
            _motionService = new MotionService(_waferViewModel, _motionViewModel);
            _visionService = new VisionService(_hikVisionHelper, _waferViewModel);
            _socketCommandService = new SocketCommandService(_waferViewModel);

            _motionService.LogPublished += WriteLog;
            _motionService.NotificationRequested += HandleMotionNotification;
            _motionService.MotionWaitStateChanged += HandleMotionWaitStateChanged;
            _motionService.PlanarMoveSafetyConfirmationRequested = HandlePlanarMoveSafetyConfirmationAsync;
            _hikVisionHelper.NotificationRequested += HandleVisionNotification;

            _worker = new AsyncPausableWorker(_waferViewModel, _hikVisionHelper)
            {
                WorkStarted = () =>
                {
                    IsWorkflowRunning = true;
                    IsWorkflowPaused = false;
                    StatusText = "运行中";
                },
                WorkStopped = () =>
                {
                    IsWorkflowRunning = false;
                    IsWorkflowPaused = false;
                    StatusText = "就绪";
                }
            };
            _motionTab = CreateMotionTab();
            _workflowTab = CreateWorkflowTab();
            RightTabs.Add(_motionTab);
            RightTabs.Add(_workflowTab);


            _waferPage = new SystemPage("晶圆图", () => WaferWorkbenchView, CreateWaferRightTabs);
            _visionPage = new SystemPage("视场", () => VisionWorkbenchView, CreateVisionRightTabs);
            Pages.Add(_waferPage);
            Pages.Add(_visionPage);
        }

        public async Task InitializeOnStartupAsync()
        {
            if (_startupInitialized || _isDisposing)
                return;

            _startupInitialized = true;
            IsStartupLoading = true;

            try
            {
                StartupLoadingMessage = "正在加载系统参数...";
                await YieldToUiAsync();
                LoadSettings();

                if (_isDisposing)
                    return;

                StartupLoadingMessage = "正在加载工作台...";
                await YieldToUiAsync();
                SelectedPage = _visionPage;
                RefreshRightTabs();

                //if (_isDisposing)
                //    return;

                //StartupLoadingMessage = "正在连接运动控制卡...";
                //await YieldToUiAsync();
                //InitializeMotionOnStartup();

                //if (_isDisposing)
                //    return;

                //StartupLoadingMessage = "正在初始化视觉模块...";
                //await YieldToUiAsync();
                //await InitializeVisionOnStartupAsync();
            }
            finally
            {
                StartupLoadingMessage = null;
                IsStartupLoading = false;
            }
        }

        partial void OnSelectedPageChanged(SystemPage value)
        {
            UpdateWorkbenchLayout();
            RefreshRightTabs();
        }

        [RelayCommand]
        private void SelectPage(SystemPage page)
        {
            if (page != null)
                SelectedPage = page;
        }

        [RelayCommand]
        private void SwapWorkbench()
        {
            if (!IsDualWorkbenchVisible)
                return;

            IsWaferWorkbenchMain = !IsWaferWorkbenchMain;
            IsVisionWorkbenchMain = !IsVisionWorkbenchMain;
            SelectedPage = IsWaferWorkbenchMain ? _waferPage : _visionPage;
            RefreshWorkbenchState();
            RefreshRightTabs();
        }

        [RelayCommand]
        private async Task StartWorkflowAsync()
        {
            if (IsWorkflowRunning)
                return;

            _worker.StartAsync();
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void PauseWorkflow()
        {
            if (!IsWorkflowRunning)
                return;

            _worker.Pause();
            IsWorkflowPaused = _worker.IsPaused;
            StatusText = IsWorkflowPaused ? "暂停" : StatusText;
        }

        [RelayCommand]
        private void ResumeWorkflow()
        {
            if (!IsWorkflowRunning)
                return;

            _worker.Resume();
            IsWorkflowPaused = _worker.IsPaused;
            StatusText = IsWorkflowPaused ? StatusText : "运行中";
        }

        [RelayCommand]
        private async Task StopWorkflowAsync()
        {
            if (!IsWorkflowRunning)
                return;

            await _worker.StopAsync();
            IsWorkflowRunning = _worker.IsRunning;
            IsWorkflowPaused = _worker.IsPaused;
            StatusText = "就绪";
        }

        [RelayCommand]
        private void StopAllAxes()
        {
            _motionViewModel.StopAllCommand.Execute(null);
        }

        [RelayCommand]
        private void LoadSettings()
        {
            var result = SystemSettingsStore.Load();
            if (result.Status == SystemSettingsLoadStatus.MissingFile)
            {
                PublishSystemLog("参数设置", "未找到配置文件，已使用默认参数。");
                SystemSettingsMapper.Apply(result.Settings, _hikVisionHelper, _waferViewModel);
                return;
            }

            if (result.Status == SystemSettingsLoadStatus.DeserializeFailed)
            {
                PublishSystemLog("参数设置", result.ErrorMessage);
                MyMessageBox.ShowError(result.ErrorMessage, "系统设置加载错误");
                return;
            }

            SystemSettingsMapper.Apply(result.Settings, _hikVisionHelper, _waferViewModel);
            PublishSystemLog("参数设置", "系统参数已加载。");
        }

        [RelayCommand]
        private void SaveSettings()
        {
            var settings = SystemSettingsMapper.Capture(_hikVisionHelper, _waferViewModel);
            if (SystemSettingsStore.Save(settings))
                PublishSystemLog("参数设置", "系统参数已保存。");
            else
                PublishSystemLog("参数设置", "系统参数保存失败。");
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var settings = SystemSettingsMapper.Capture(_hikVisionHelper, _waferViewModel);
            var viewModel = SettingsViewModel.FromSettings(settings);
            var window = new SettingsWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };

            if (window.ShowDialog() != true)
                return;

            SystemSettings updated = viewModel.ToSettings();
            SystemSettingsMapper.Apply(updated, _hikVisionHelper, _waferViewModel);
            if (!SystemSettingsStore.Save(updated))
            {
                PublishSystemLog("参数设置", "设置已应用，但保存配置文件失败。");
                return;
            }

            PublishSystemLog("参数设置", "设置已应用并保存。");
        }

        [RelayCommand]
        private void OpenProject()
        {
            var window = new ProjectWindow
            {
                Owner = Application.Current?.MainWindow
            };
            window.ShowDialog();
        }

        [RelayCommand]
        private void ClearLog()
        {
            Logs.Clear();
        }

        [RelayCommand]
        private void CloseApplication()
        {
            Application.Current?.MainWindow?.Close();
        }

        [RelayCommand]
        private void DisconnectSocketDevice()
        {
            _socketStopListeningAction?.Invoke();
        }

        private TabItem CreateWorkflowTab()
        {
            return new TabItem
            {
                Header = "流程控制",
                Content = new WorkflowControlView
                {
                    DataContext = this
                }
            };
        }

        private TabItem CreateMotionTab()
        {
            return new TabItem
            {
                Header = "移动控制",
                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = new MotionConfigView
                    {
                        DataContext = _motionViewModel
                    }
                }
            };
        }

        private IReadOnlyList<TabItem> CreateWaferRightTabs()
        {
            return new[]
            {
                CreateRightTab("配置", new WaferConfigView { DataContext = _waferViewModel }),
                CreateRightTab("分档", new BinsConfigView { DataContext = _waferViewModel })
            };
        }

        private IReadOnlyList<TabItem> CreateVisionRightTabs()
        {
            var visionConfig = new VisionConfigView(_hikVisionHelper);
            _visionService.BindVisionConfig(visionConfig);

            return new[]
            {
                CreateRightTab("配置", visionConfig)
            };
        }

        private UserControl CreateVisionMainView()
        {
            var visionMainView = new VisionMainView(_hikVisionHelper);
            _visionService.BindVisionMain(visionMainView);
            return visionMainView;
        }

        private WaferMainView GetWaferWorkbenchView()
        {
            _waferWorkbenchView ??= new WaferMainView(_waferViewModel);
            return _waferWorkbenchView;
        }

        private VisionMainView GetVisionWorkbenchView()
        {
            _visionWorkbenchView ??= (VisionMainView)CreateVisionMainView();
            return _visionWorkbenchView;
        }

        private static TabItem CreateRightTab(string header, object content)
        {
            return new TabItem
            {
                Header = header,
                Content = content
            };
        }

        private void RefreshRightTabs()
        {
            if (_workflowTab == null || _motionTab == null)
                return;

            EnsureFixedRightTabs();

            while (RightTabs.Count > 2)
                RightTabs.RemoveAt(2);

            if (SelectedPage?.RightTabs == null)
                return;

            foreach (var tab in SelectedPage.RightTabs)
                RightTabs.Add(tab);
        }

        private void UpdateWorkbenchLayout()
        {
            if (SelectedPage == null)
                return;

            if (ReferenceEquals(SelectedPage, _waferPage))
            {
                GetWaferWorkbenchView();
                GetVisionWorkbenchView();
                IsDualWorkbenchVisible = true;
                IsWaferWorkbenchMain = true;
                IsVisionWorkbenchMain = false;
                GeneralWorkbenchView = null;
            }
            else if (ReferenceEquals(SelectedPage, _visionPage))
            {
                GetWaferWorkbenchView();
                GetVisionWorkbenchView();
                IsDualWorkbenchVisible = true;
                IsWaferWorkbenchMain = false;
                IsVisionWorkbenchMain = true;
                GeneralWorkbenchView = null;
            }
            else
            {
                IsDualWorkbenchVisible = false;
                GeneralWorkbenchView = SelectedPage.Content;
            }

            RefreshWorkbenchState();
        }

        private void RefreshWorkbenchState()
        {
            WorkbenchSwitcherVisibility = IsDualWorkbenchVisible ? Visibility.Visible : Visibility.Collapsed;
            if (_waferWorkbenchView != null)
                _waferWorkbenchView.IsCompact = IsWaferWorkbenchMini;
            if (_visionWorkbenchView != null)
                _visionWorkbenchView.IsCompact = IsVisionWorkbenchMini;

            OnPropertyChanged(nameof(IsWaferWorkbenchVisible));
            OnPropertyChanged(nameof(IsVisionWorkbenchVisible));
            OnPropertyChanged(nameof(IsGeneralWorkbenchVisible));
            OnPropertyChanged(nameof(IsWaferWorkbenchMini));
            OnPropertyChanged(nameof(IsVisionWorkbenchMini));
            OnPropertyChanged(nameof(WaferViewZIndex));
            OnPropertyChanged(nameof(VisionViewZIndex));
        }

        private void EnsureFixedRightTabs()
        {
            if (RightTabs.Count == 0)
            {
                RightTabs.Add(_motionTab);
                RightTabs.Add(_workflowTab);
                return;
            }
            if (!ReferenceEquals(RightTabs[0], _motionTab))
            {
                RightTabs.Remove(_motionTab);
                RightTabs.Insert(1, _motionTab);
            }
            if (RightTabs.Count == 1)
            {
                RightTabs.Add(_motionTab);
                return;
            }
            if (!ReferenceEquals(RightTabs[1], _workflowTab))
            {
                RightTabs.Remove(_workflowTab);
                RightTabs.Insert(0, _workflowTab);
            }
        }
            

        private void WriteLog(string module, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            WriteLogFile(module, message);

            void Append()
            {
                Logs.Insert(0, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} --- {module} : {message}");
                while (Logs.Count > 50)
                    Logs.RemoveAt(Logs.Count - 1);
            }

            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                Append();
            else
                dispatcher.BeginInvoke((Action)Append, DispatcherPriority.Background);
        }

        private static void WriteLogFile(string module, string message)
        {
            try
            {
                MyLog.WriteLog(string.IsNullOrWhiteSpace(module) ? "WaferSystem-wpf" : module, message);
            }
            catch
            {
                // Do not let file logging failures interrupt runtime logging.
            }
        }

        private void PublishSystemLog(string module, string message)
        {
            WriteLog(module, message);
            RuntimeLogMessenger.Broadcast("WaferSystem-wpf", module, message);
        }

        private void HandleRuntimeLog(RuntimeLogMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Message))
                return;

            if (string.Equals(message.Project, "WaferSystem-wpf", StringComparison.Ordinal))
                return;

            string module = string.IsNullOrWhiteSpace(message.Module)
                ? message.Project
                : message.Module;
            WriteLog(module, message.Message);
        }

        private void HandleSocketDeviceConnected()
        {
            IsWorkflowInteractionLocked = true;
        }

        private void HandleSocketDeviceDisconnected()
        {
            IsWorkflowInteractionLocked = false;
        }

        private void BindSocketStopListening(Action stopListeningAction)
        {
            _socketStopListeningAction = stopListeningAction;
        }

        private Task<bool> HandlePlanarMoveSafetyConfirmationAsync(PlanarMoveSafetyRequest request)
        {
            if (_isDisposing)
                return Task.FromResult(false);

            string message =
                "检测到Z轴当前处于扎针危险高度，本次XY/T移动已取消。\r\n" +
                "取消或关闭不执行任何动作；点击确定仅移动Z轴到安全高度。\r\n\r\n" +
                $"当前Z高度：{request.CurrentZHeight:F3}\r\n" +
                $"扎针高度：{request.ContactHeight:F3}\r\n" +
                $"安全高度：{request.SafetyHeight:F3}";

            var result = MyMessageBox.ShowQuery(message, "移动安全提示");
            return Task.FromResult(result == MyDialogResult.OK);
        }

        private void HandleMotionWaitStateChanged(bool isWaiting, string message)
        {
            void Apply()
            {
                MotionViewModel.IsInitializing = isWaiting;
                MotionViewModel.InitializingMessage = isWaiting
                    ? (string.IsNullOrWhiteSpace(message) ? "请等待当前操作完成..." : message)
                    : null;
            }

            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                Apply();
            else
                dispatcher.BeginInvoke((Action)Apply, DispatcherPriority.Background);
        }

        private void InitializeMotionOnStartup()
        {
            if (_isDisposing)
                return;

            _motionService.Initialize();
        }

        private async Task InitializeVisionOnStartupAsync()
        {
            try
            {
                await Task.Yield();
                if (_isDisposing)
                    return;

                GetVisionWorkbenchView();
                if (_isDisposing)
                    return;

                await _visionService.InitializeAsync();
            }
            catch (Exception ex)
            {
                if (_isDisposing)
                    return;

                string message = "启动时视觉初始化异常：" + ex.Message;
                _hikVisionHelper.OnLogMessage(message);
                _hikVisionHelper.RequestNotification(VisionNotificationKind.Error, message, "视觉初始化异常");
            }
        }

        private static Task YieldToUiAsync()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return Task.CompletedTask;

            return dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task;
        }

        private void HandleMotionNotification(MotionNotificationKind kind, string message, string title)
        {
            if (_isDisposing)
                return;

            switch (kind)
            {
                case MotionNotificationKind.Error:
                    MyMessageBox.ShowError(message, title);
                    break;
                case MotionNotificationKind.Warning:
                    MyMessageBox.ShowWarn(message, title);
                    break;
            }
        }

        private void HandleVisionNotification(VisionNotificationKind kind, string message, string title)
        {
            if (_isDisposing)
                return;

            switch (kind)
            {
                case VisionNotificationKind.Info:
                    MyMessageBox.ShowInfo(message, title);
                    break;
                case VisionNotificationKind.Success:
                    MyMessageBox.ShowSuccess(message, title);
                    break;
                case VisionNotificationKind.Error:
                    MyMessageBox.ShowError(message, title);
                    break;
                case VisionNotificationKind.Warning:
                    MyMessageBox.ShowWarn(message, title);
                    break;
            }
        }


        public void Dispose()
        {
            if (_isDisposing)
                return;

            _isDisposing = true;
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _motionService.MotionWaitStateChanged -= HandleMotionWaitStateChanged;
            _motionService.PlanarMoveSafetyConfirmationRequested = null;
            _worker.Dispose();
            _visionService.Dispose();
            _motionService.Dispose();
        }
    }
    public partial class SystemPage : ObservableObject
    {
        private readonly Func<UserControl> _contentFactory;
        private readonly Func<IReadOnlyList<TabItem>> _rightTabsFactory;
        private UserControl _content;
        private IReadOnlyList<TabItem> _rightTabs;

        public SystemPage(string title, Func<UserControl> contentFactory, Func<IReadOnlyList<TabItem>> rightTabsFactory = null)
        {
            Title = title;
            _contentFactory = contentFactory ?? throw new ArgumentNullException(nameof(contentFactory));
            _rightTabsFactory = rightTabsFactory;
        }

        public string Title { get; }

        public UserControl Content => _content ??= _contentFactory();
        public IReadOnlyList<TabItem> RightTabs => _rightTabs ??= _rightTabsFactory?.Invoke();
    }
}
