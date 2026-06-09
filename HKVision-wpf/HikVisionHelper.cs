using HKVision.Wpf.Model;
using HKVision.Wpf.Services;
using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HKVision.Wpf
{
    public partial class HikVisionHelper
    {
        public VisionDataModel DataModel { get; } = new VisionDataModel();

        public VmSolutionManager VmManager { get; }

        private readonly VisionDataClient _dataClient;

        public event Action<VisionNotificationKind, string, string> NotificationRequested;

        public HikVisionHelper(VmSolutionManager vmManager = null, VisionDataClient dataClient = null)
        {
            _dataClient = dataClient ?? new VisionDataClient(DataModel);
            VmManager = vmManager ?? new VmSolutionManager(DataModel);
        }

        public async Task<Point2D?> ReadServerCenterDataAsync()
        {
            var list = await ReadServerAsync();
            if (list == null || list.Count == 0)
                return null;
            if (list.Count == 1)
                return list[0];

            double centerX = DataModel.CameraSize.X / 2d;
            double centerY = DataModel.CameraSize.Y / 2d;

            Point2D nearestPoint = list[0];
            double minDistanceSquared = GetDistanceSquared(nearestPoint, centerX, centerY);

            for (int i = 1; i < list.Count; i++)
            {
                Point2D currentPoint = list[i];
                double currentDistanceSquared = GetDistanceSquared(currentPoint, centerX, centerY);
                if (currentDistanceSquared < minDistanceSquared)
                {
                    nearestPoint = currentPoint;
                    minDistanceSquared = currentDistanceSquared;
                }
            }

            return nearestPoint;
        }

        public async Task<List<Point2D>> ReadServerAsync()
        {
            if (!VmManager.IsConnect)
            {
                OnLogMessage("请先链接相机设备");
                return null;
            }
            if (!VmManager.IsContinuousRun())
            {
                VmManager.ContinueRun();
                await Task.Delay(20);
            }

            var result = await _dataClient.ReadServerAsync();
            if (result.IsFailure)
            {
                OnLogMessage($"获取匹配信息失败--- {result.Message}");
                return null;
            }

            if (result.Data.ToUpperInvariant().Contains("NG"))
            {
                OnLogMessage("未找到匹配项");
                return null;
            }

            return _dataClient.ParseMatchInfo(result.Data);
        }

        public void Disconnect()
        {
            VmManager.DiscontinueRun();
            _dataClient.StopListen();
            StopScanning();
            DataModel.OnLogMessage("相机及视觉服务已彻底断开/停止");
        }

        public void OnLogMessage(string message)
        {
            DataModel.OnLogMessage(message);
        }

        public void RequestNotification(VisionNotificationKind kind, string message, string title)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            NotificationRequested?.Invoke(kind, message, title);
        }

        private static double GetDistanceSquared(Point2D point, double centerX, double centerY)
        {
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            return dx * dx + dy * dy;
        }
    }

    public enum VisionNotificationKind
    {
        Info,
        Success,
        Error,
        Warning
    }
}
