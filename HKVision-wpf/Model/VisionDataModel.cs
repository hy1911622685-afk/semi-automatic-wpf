using Newtonsoft.Json;
using MyAsset.Wpf.Messaging;
using System;

namespace HKVision.Wpf.Model
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Message { get; }

        protected Result(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static Result Success() => new Result(true, string.Empty);
        public static Result Fail(string message) => new Result(false, message);
    }

    public class Result<T> : Result
    {
        public T Data { get; }

        private Result(bool isSuccess, T data, string message) : base(isSuccess, message)
        {
            Data = data;
        }

        public static Result<T> Success(T data) => new Result<T>(true, data, string.Empty);
        public new static Result<T> Fail(string message) => new Result<T>(false, default, message);
    }

    [Serializable]
    public class VisionDataModel
    {
        public string VmServerIp { get; set; } = "127.0.0.1";
        public int VmServerPort { get; set; } = 7930;

        private (int X, int Y) _cameraSize = (2440, 2048);

        public (int X, int Y) CameraSize
        {
            get => _cameraSize;
            set
            {
                _cameraSize = value;
                Transformer?.UpdateCameraSize(value.X, value.Y);
            }
        }

        public CoordinateTransformer Transformer { get; }

        public int AllowableErrorNumber { get; set; } = 2;
        public double MatchScore { get; set; } = 0.7;
        public double AoiMatchScore { get; set; } = 0.60;
        public double AOIScanStepX { get; set; } = 4d;
        public double AOIScanStepY { get; set; } = 4d;
        public double DieDimensionScanStep { get; set; } = 0.05;

        public VisionDataModel()
        {
            Transformer = new CoordinateTransformer(_cameraSize.X, _cameraSize.Y);
        }

        public void OnLogMessage(string message)
        {
            RuntimeLogMessenger.Broadcast("HKVision-wpf", "相机模块", message);
        }
    }

    public enum VisionMoveDir
    {
        Right,
        Front,
        Left,
        Back
    }
}
