using MyAsset.Wpf.Infrastructure;
using HKVision.Wpf.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HKVision.Wpf.Services
{
    public class VisionDataClient
    {
        private readonly VisionDataModel _dataModel;
        private CancellationTokenSource _ctsListen;

        public VisionDataClient(VisionDataModel dataModel)
        {
            _dataModel = dataModel;
        }

        public async Task<Result<string>> ReadServerAsync(int timeoutMs = 5000)
        {
            StopListen();
            _ctsListen = new CancellationTokenSource();

            using (var timeoutCts = new CancellationTokenSource(timeoutMs))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_ctsListen.Token, timeoutCts.Token))
            {
                try
                {
                    using (var client = new TcpClient())
                    using (linkedCts.Token.Register(client.Close))
                    {
                        await client.ConnectAsync(_dataModel.VmServerIp, _dataModel.VmServerPort);
                        linkedCts.Token.ThrowIfCancellationRequested();

                        using (var stream = client.GetStream())
                        {
                            byte[] buffer = new byte[2048];
                            int read = await stream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token);

                            return read > 0
                                ? Result<string>.Success(Encoding.UTF8.GetString(buffer, 0, read))
                                : Result<string>.Fail("收到空数据");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return _ctsListen.Token.IsCancellationRequested
                        ? Result<string>.Fail("读取操作被手动中止")
                        : Result<string>.Fail("读取数据超时");
                }
                catch (Exception ex)
                {
                    return Result<string>.Fail($"通讯异常: {ex.Message}");
                }
            }
        }

        public void StopListen()
        {
            _ctsListen?.Cancel();
        }

        public List<Point2D> ParseMatchInfo(string input)
        {
            if (string.IsNullOrEmpty(input) || !input.StartsWith("Num"))
                return null;

            int firstNumIndex = input.IndexOf("Num:", StringComparison.OrdinalIgnoreCase);
            if (firstNumIndex == -1)
                return null;

            int secondNumIndex = input.IndexOf("Num:", firstNumIndex + 4, StringComparison.OrdinalIgnoreCase);
            if (secondNumIndex != -1)
                input = input.Substring(0, secondNumIndex);

            string[] partAry = input.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            string numValue = partAry[0].Replace("Num:", "").Trim();

            if (!int.TryParse(numValue, out int totalCount) || totalCount <= 0)
                return null;

            return partAry
                .Skip(1)
                .Select(ParseCoordinate)
                .Where(pos => pos.HasValue)
                .Select(pos => new Point2D { X = pos.Value.X, Y = pos.Value.Y })
                .ToList();
        }

        private (double X, double Y)? ParseCoordinate(string coordinateString)
        {
            if (string.IsNullOrWhiteSpace(coordinateString))
                return null;

            double xValue = 0;
            double yValue = 0;
            bool hasX = false;
            bool hasY = false;

            var pairs = coordinateString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string pair in pairs)
            {
                var kv = pair.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (kv.Length < 2)
                    continue;

                string key = kv[0].Trim().ToUpperInvariant();
                string value = kv[1].Trim();

                if (key == "X" && double.TryParse(value, out xValue))
                    hasX = true;
                else if (key == "Y" && double.TryParse(value, out yValue))
                    hasY = true;
            }

            return hasX && hasY ? (xValue, yValue) : ((double X, double Y)?)null;
        }
    }
}
