using System;

namespace Bing.Utils.Date.Chinese
{
    /// <summary>
    /// 中国生肖操作辅助类
    /// </summary>
    public static class ChineseAnimalHelper
    {
        /// <summary>
        /// 生肖 -简体
        /// </summary>
        private static readonly string[] Animals = { "鼠", "牛", "虎", "兔", "龙", "蛇", "马", "羊", "猴", "鸡", "狗", "猪" };

        /// <summary>
        /// 生肖 - 繁体
        /// </summary>
        private static readonly string[] Animalz = { "鼠", "牛", "虎", "兔", "龍", "蛇", "馬", "羊", "猴", "雞", "狗", "豬" };

        /// <summary>
        /// 生肖开始年份。1900年为鼠年
        /// </summary>
        private const int AnimalStartYear = 1900;

        /// <summary>
        /// 获取指定时间所在生肖索引
        /// </summary>
        /// <param name="dt">时间</param>
        private static int Index(DateTime dt)
        {
            var offset = dt.Year - AnimalStartYear;
            return offset % 12;
        }

        /// <summary>
        /// 获取生肖
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="traditionalChineseCharacter">是否繁体中文</param>
        public static string Get(DateTime dt, bool traditionalChineseCharacter = false)
        {
            var animals = traditionalChineseCharacter ? Animalz : Animals;
            return animals[Index(dt)];
        }
    }
}
