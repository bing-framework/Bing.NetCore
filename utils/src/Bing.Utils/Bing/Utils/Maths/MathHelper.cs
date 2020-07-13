using System;

namespace Bing.Utils.Maths
{
    /// <summary>
    /// 数据计算操作辅助类
    /// </summary>
    public static class MathHelper
    {
        #region GetDistance(获取两点之间的距离)

        /// <summary>
        /// 获取两点之间的距离
        /// </summary>
        /// <param name="x1">横坐标1</param>
        /// <param name="y1">纵坐标1</param>
        /// <param name="x2">横坐标2</param>
        /// <param name="y2">纵坐标2</param>
        public static double GetDistance(double x1, double y1, double x2, double y2) => Math.Sqrt(Math.Pow((x1 - x2), 2) + Math.Pow((y1 - y2), 2));

        #endregion
    }
}
