using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyAsset.Wpf.Infrastructure;
namespace MotionCard.Model
{
    public class EccentricityCompensator
    {
        public Point2D AxisCenter  = new Point2D(93.098, -68.473); // 机械轴旋转中心坐标
        public Point2D WaferCenter  = new Point2D(93.322, -68.319); // wafer 物理旋转中心坐标

        public Point2D FirstDie = new Point2D(93.341, -78.028); // 第一个Die扎针位置


        /// <summary>
        /// 专为 [Y轴向下递增] + [载物台移动] 的机台定制的偏心补偿算法
        /// </summary>
        /// <param name="chuckCenter">Chuck旋转中心</param>
        /// <param name="waferCenter">晶圆中心</param>
        /// <param name="physicalAngle">物理旋转角度（绝对值）</param>
        /// <param name="isClockwise">物理旋转方向：true为顺时针，false为逆时针</param>
        /// <returns>返回XY轴直接需要走行的补偿量 (DeltaX, DeltaY)</returns>
        public (double MoveX, double MoveY) GetStageCompensationTarget(
            double physicalAngle,
            bool isClockwise)
        {
            // 1. 计算偏心向量 (保持不变)
            double ex = WaferCenter.X - AxisCenter.X;
            double ey = WaferCenter.Y - AxisCenter.Y;

            // 2. ★ 核心修正：Y向下递增的坐标系，顺时针对应正角度，逆时针对应负角度
            double mathAngle = Math.Abs(physicalAngle);
            if (!isClockwise)
            {
                mathAngle = -mathAngle; // 逆时针
            }

            // 3. 转弧度并计算三角函数
            double rad = mathAngle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            // 4. 计算晶圆的绝对物理位移
            double physDeltaX = ex * (cos - 1) - ey * sin;
            double physDeltaY = ex * sin + ey * (cos - 1);

            // 5. ★ 核心修正：载物台动，需要反向补偿，所以直接加个负号
            double compMoveX = -physDeltaX;
            double compMoveY = -physDeltaY;

            return (compMoveX, compMoveY);
        }



        public (double MoveX, double MoveY) CompensationTarget(double angle, bool clockwise)
        {
            //var waferCenterToFirstDieRadius = CalculateDistance((WaferCenter.X, WaferCenter.Y), (FirstDie.X, FirstDie.Y)); // 计算从晶圆中心到第一个Die的距离（半径）

            // 根据旋转角度和半径计算第一个Die旋转后的新位置
            var nextDie = GetOriginalPosition(WaferCenter.X, WaferCenter.Y, FirstDie.X, FirstDie.Y, angle, clockwise);

            //var AxisToFirstDieRadius = CalculateDistance((AxisCenter.X, AxisCenter.Y), (nextDie.X, nextDie.Y));

            var nextAxis = GetOriginalPosition(AxisCenter.X, AxisCenter.Y, nextDie.X, nextDie.Y, angle, !clockwise);

            return (nextAxis.X - FirstDie.X, nextAxis.Y - FirstDie.Y);
        }

        /// <summary>
        /// 计算两点之间的欧几里得距离
        /// </summary>
        /// <param name="p1">起点 (x, y)</param>
        /// <param name="p2">终点 (x, y)</param>
        /// <returns>两点间的距离</returns>
        public double CalculateDistance((double X, double Y) p1, (double x, double y) p2)
        {
            // 计算差值
            double dx = p2.x - p1.X;
            double dy = p2.y - p1.Y;

            // 使用勾股定理计算距离
            // Math.Sqrt 是开平方根，Math.Pow 是幂运算
            return Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
        }

        /// <summary>
        /// 根据中心点、半径和角度计算目标点坐标
        /// </summary>
        /// <param name="centerX">中心点 X</param>
        /// <param name="centerY">中心点 Y</param>
        /// <param name="radius">半径</param>
        /// <param name="angleInDegrees">角度 (0-360)</param>
        /// <returns>目标坐标 (x, y)</returns>
        public (double X, double Y) CalculatePoint(double centerX, double centerY, double radius, double angleInDegrees)
        {
            angleInDegrees = angleInDegrees + 360;
            // 1. 将角度转换为弧度
            double angleInRadians = angleInDegrees * (Math.PI / 180.0);

            // 2. 使用三角函数计算偏移量
            // Math.Cos 对应 X 轴（邻边）
            // Math.Sin 对应 Y 轴（对边）
            double x = centerX + radius * Math.Cos(angleInRadians);
            double y = centerY + radius * Math.Sin(angleInRadians);

            return (x, y);
        }


        /// <summary>
        /// 计算在绕指定中心旋转指定角度后，移动到目标点的原始点坐标。
        /// 即：已知旋转后位置 target，求旋转前位置 original。
        /// </summary>
        /// <param name="centerX">旋转中心 X</param>
        /// <param name="centerY">旋转中心 Y</param>
        /// <param name="targetX">旋转后到达点的 X（例如视场中心）</param>
        /// <param name="targetY">旋转后到达点的 Y</param>
        /// <param name="angleDegrees">旋转角度（绝对值，单位度）</param>
        /// <param name="clockwise">旋转方向：true = 顺时针，false = 逆时针</param>
        /// <returns>原始点坐标 (X, Y)</returns>
        public static (double X, double Y) GetOriginalPosition(
            double centerX, double centerY,
            double targetX, double targetY,
            double angleDegrees,
            bool clockwise)
        {
            // 根据方向确定实际旋转角度（逆时针为正）
            double effectiveAngle = clockwise ? -angleDegrees : angleDegrees;
            // 转换为弧度
            double angleRad = effectiveAngle * Math.PI / 180.0;
            // 逆旋转角度（求原始点需要反向旋转）
            double cos = Math.Cos(-angleRad);
            double sin = Math.Sin(-angleRad);
            double dx = targetX - centerX;
            double dy = targetY - centerY;
            double originalX = centerX + dx * cos - dy * sin;
            double originalY = centerY + dx * sin + dy * cos;
            return (originalX, originalY);
        }
        /*
         * 
         * | 调用方式 | 实际计算效果 |
          |----------|--------------|
          | `GetOriginalPosition(..., 90, true)` | 顺时针旋转90°的原始位置 |
          | `GetOriginalPosition(..., 90, false)` | 逆时针旋转90°的原始位置 |
          | `GetOriginalPosition(..., -90, true)` | **变成了逆时针旋转90°**（因为 `-90 → effectiveAngle=90 → 反向旋    转-90   =    顺 时针反向 → 实际为逆时针） |
          | `GetOriginalPosition(..., -90, false)` | `effectiveAngle = -90` → 反向旋转 `90°` → 实际为顺时针旋转90° |
         */
    }
}
