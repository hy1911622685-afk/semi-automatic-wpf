namespace MotionCard
{
    public enum MotionWorkMode
    {
        Normal,
        Testing
    }

    public static class MotionRuntimeState
    {
        private static readonly object LockObject = new object();
        private static MotionWorkMode _workMode = MotionWorkMode.Normal;

        public static MotionWorkMode WorkMode
        {
            get
            {
                lock (LockObject)
                {
                    return _workMode;
                }
            }
            set
            {
                lock (LockObject)
                {
                    _workMode = value;
                }
            }
        }

        public static void SetNormal()
        {
            WorkMode = MotionWorkMode.Normal;
        }

        public static void SetTesting()
        {
            WorkMode = MotionWorkMode.Testing;
        }
    }
}
