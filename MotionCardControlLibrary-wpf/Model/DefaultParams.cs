using System.ComponentModel;

namespace MotionCard
{
    // ================= [新增] 运动控制标准返回值 =================
    public class MotionResult
    {
        public const short Success = 0; // 正常/成功 (与绝大多数板卡API一致)

        // 软件层自定义错误码 (使用极小的负数，避免与底层硬件报错码如 1, -1, -5 等冲突)
        public const short ErrStateInvalid = -1001;  // 状态异常 (如急停、报错时拒绝指令)
        public const short ErrLockTimeout = -1002;   // 并发锁超时 (轴被其他任务一直占用)
        public const short ErrCanceled = -1003;      // 运动被上层强行取消
        public const short ErrMotionStartFailed = -1004; // 参数与命令链路已走通，但轴未进入预期的运行状态
        public const short ErrConnectFailed = -1005; // 控制卡连接失败
    }

    // ================= [新增] 轴状态机枚举 =================
    public enum AxisState
    {
        Idle,               // 空闲，可接收新指令
        Moving,             // 运动中，普通指令将排队或被拒绝
        Homing,             // 回原点中
        Error,              // 伺服报警/硬限位触发（必须手动复位）
        EmergencyStopped    // 急停锁定状态（必须手动解除）
    }
    public static class AxisInfo
    {
        public static ushort CardNum = 0;//控制卡数量\
        public static uint[] CardTypeList = new uint[8] { 0, 0, 0, 0, 0, 0, 0, 0 }; //控制卡固件类型
        public static ushort[] CardIdList = new ushort[8] { 0, 0, 0, 0, 0, 0, 0, 0 };  //控制卡硬件ID号
        public static ushort CardNo; //控制卡卡号
        //public static short CardVersion; //控制卡硬件版本号
        //public static short FirmID; //控制卡固件类型
        //public static short SubFirmID;//控制卡固件版本号
        //public static short LibVer;//控制卡库版本号
        //public static ushort CardID; //卡ID
        public static bool IsRun = false; //是否运行中
        public static bool IsEmgStop = false; //是否按下急停
        public static bool IsConnect = false; //是否运行中
        public static uint TotalAxis; //当前卡轴数
        public static double Acc = 0.1; //加速时间
        public static double Dec = 0.1; //减速时间

        public static bool IsRightLimit = false;
        public static bool IsFrontLimit = false;
        public static bool IsLeftLimit = false;
        public static bool IsBackLimit = false;
        public static bool IsTopLimit = false;
        public static bool IsBottomLimit = false;
    }
    public class AxisType
    {
        public const ushort AxisX = 0;
        public const ushort AxisY = 1;
        public const ushort AxisZ = 2;
        public const ushort AxisT = 3;
    }

    public class PosiMode
    {
        /// <summary>
        /// 相对运动
        /// </summary>
        public const ushort RelativeMotion = 0;
        /// <summary>
        /// 绝对运动
        /// </summary>
        public const ushort AbsoluteMotion = 1;
    }

    public enum AxisDirType
    {
        Up,
        Down,
        Front,
        Back,
        Left,
        Right,
        /// <summary>
        /// T-顺时针
        /// </summary>
        Redo,
        /// <summary>
        /// T-逆时针
        /// </summary>
        Undo
    }

    public enum SpeedType
    {
        [Description("回零")]
        Zero,
        [Description("快速")]
        Fast,
        [Description("正常")]
        Normal,
        [Description("慢速")]
        Slow,
    }

    public enum ZAxisHeightEnum
    {
        [Description("默认的安全高度")]
        Safety,

        [Description("分离高度")]
        Separation,

        [Description("接触高度")]
        Contact
    }
    //Z轴的各种运动位置及方式
    //Mode=0，运动到分离高度，即测试高度
    //Mode=1，运动到接触高度
    //Mode=2，运动到对准高度
    //Mode=3，运动到接片高度
    //Mode=4，运动到接片加抬升高度
    //Mode=5，空
    //Mode=6，运动到电气零位高度
    //Mode=7，Z轴微动增加一个微步1微米，测试高度变化
    //Mode=8，Z轴微动减少一个微步1微米，测试高度变化


}
