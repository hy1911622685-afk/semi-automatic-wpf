using System.Collections.Generic;

namespace MotionCard
{
    public class ZAxisHeightConfig
    {
        // 使用字典存储高度，默认值直接在这里初始化
        private static readonly Dictionary<ZAxisHeightEnum, double> Heights = new Dictionary<ZAxisHeightEnum, double>()
        {
            { ZAxisHeightEnum.Safety, 25 },
            { ZAxisHeightEnum.Separation, 35 },
            { ZAxisHeightEnum.Contact, 36 }
        };

        // 统一的访问接口
        public static double GetHeight(ZAxisHeightEnum type) => Heights[type];

        // 如果需要运行时修改高度
        // 5. 更新高度的方法
        public static void Update(ZAxisHeightEnum type, double value)
        {
            if (Heights.ContainsKey(type))
            {
                Heights[type] = value;
            }
        }
    }

}
