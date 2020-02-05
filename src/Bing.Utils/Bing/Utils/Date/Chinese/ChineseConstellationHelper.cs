using System;

namespace Bing.Utils.Date.Chinese
{
    /// <summary>
    /// 二十八星宿操作辅助类
    /// </summary>
    public static class ChineseConstellationHelper
    {
        /// <summary>
        /// 星宿 - 简体
        /// </summary>
        private static readonly string[] Names =
        {
            //四        五      六         日        一      二      三  
            "角木蛟", "亢金龙", "女土蝠", "房日兔", "心月狐", "尾火虎", "箕水豹",
            "斗木獬", "牛金牛", "氐土貉", "虚日鼠", "危月燕", "室火猪", "壁水獝",
            "奎木狼", "娄金狗", "胃土彘", "昴日鸡", "毕月乌", "觜火猴", "参水猿",
            "井木犴", "鬼金羊", "柳土獐", "星日马", "张月鹿", "翼火蛇", "轸水蚓"
        };

        /// <summary>
        /// 星宿 - 繁体
        /// </summary>
        private static readonly string[] Namez =
        {
            "角木蛟", "亢金龍", "女土蝠", "房日兔", "心月狐", "尾火虎", "箕水豹",
            "鬥木獬", "牛金牛", "氐土貉", "虛日鼠", "危月燕", "室火豬", "壁水獝",
            "奎木狼", "婁金狗", "胃土彘", "昴日雞", "畢月烏", "觜火猴", "參水猿",
            "井木犴", "鬼金羊", "柳土獐", "星日馬", "張月鹿", "翼火蛇", "軫水蚓"
        };

        /// <summary>
        /// 二十八星宿参考值，本日为角
        /// </summary>
        private static readonly DateTime ChineseConstellationReferDay = new DateTime(2007, 9, 13);

        /// <summary>
        /// 获取二十八星宿
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="traditionalChineseCharacter">是否繁体中文</param>
        public static string Get(DateTime dt, bool traditionalChineseCharacter = false)
        {
            var dict = traditionalChineseCharacter ? Namez : Names;
            var offset = (dt - ChineseConstellationReferDay).Days;
            var modStarDay = offset % 28;
            return modStarDay >= 0 ? dict[modStarDay] : dict[27 + modStarDay];
        }
    }
}
