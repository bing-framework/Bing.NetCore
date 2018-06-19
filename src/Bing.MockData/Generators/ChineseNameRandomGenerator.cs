using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Core;

namespace Bing.MockData.Generators
{
    /// <summary>
    /// 姓名生成器
    /// </summary>
    public class ChineseNameRandomGenerator:RandomGeneratorBase,IRandomGenerator
    {
        /// <summary>
        /// 姓氏
        /// </summary>
        private static readonly string[] FirstName = new string[]
        {
            "李", "王", "张",
            "刘", "陈", "杨", "黄", "赵", "周", "吴", "徐", "孙", "朱", "马", "胡", "郭", "林",
            "何", "高", "梁", "郑", "罗", "宋", "谢", "唐", "韩", "曹", "许", "邓", "萧", "冯",
            "曾", "程", "蔡", "彭", "潘", "袁", "於", "董", "余", "苏", "叶", "吕", "魏", "蒋",
            "田", "杜", "丁", "沈", "姜", "范", "江", "傅", "钟", "卢", "汪", "戴", "崔", "任",
            "陆", "廖", "姚", "方", "金", "邱", "夏", "谭", "韦", "贾", "邹", "石", "熊", "孟",
            "秦", "阎", "薛", "侯", "雷", "白", "龙", "段", "郝", "孔", "邵", "史", "毛", "常",
            "万", "顾", "赖", "武", "康", "贺", "严", "尹", "钱", "施", "牛", "洪", "龚", "东方",
            "夏侯", "诸葛", "尉迟", "皇甫", "宇文", "鲜于", "西门", "司马", "独孤", "公孙", "慕容", "轩辕",
            "左丘", "欧阳", "上官", "闾丘", "令狐", "太史", "端木", "东方", "南宫", "万俟", "闻人",
            "公羊", "赫连", "澹台", "宗政", "濮阳", "公冶", "太叔", "申屠", "仲孙", "钟离", "长孙", "司徒", "司空",
            "闾丘", "子车", "亓官", "司寇", "巫马", "公西", "颛孙", "壤驷", "公良", "漆雕", "乐正", "宰父", "谷梁", "拓跋", "夹谷", "段干",
            "百里", "呼延", "东郭", "南门", "羊舌", "微生", "公户", "公玉", "公仪", "梁丘", "公仲", "公上", "公门", "公山", "公坚", "公伯",
            "公祖", "第五", "公乘", "贯丘", "公皙", "南荣", "东里", "东宫", "仲长", "子书", "子桑", "即墨", "达奚", "褚师", "吴铭"
        };

        /// <summary>
        /// 实例
        /// </summary>
        public static readonly ChineseNameRandomGenerator Instance = new ChineseNameRandomGenerator();

        /// <summary>
        /// 初始化一个<see cref="ChineseNameRandomGenerator"/>类型的实例
        /// </summary>
        private ChineseNameRandomGenerator() { }

        /// <summary>
        /// 生成姓名
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            return BatchGenerate(1).First();
        }

        /// <summary>
        /// 批量生成姓名
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        public override List<string> BatchGenerate(int maxLength)
        {
            List<string> list=new List<string>();
            for (int i = 0; i < maxLength; i++)
            {
                string name = GetFirstName() + Builder.GenerateRandomLengthChinese(3);
                list.Add(name);
            }

            return list;
        }

        /// <summary>
        /// 获取 姓氏
        /// </summary>
        /// <returns></returns>
        private string GetFirstName()
        {
            return FirstName[Builder.GenerateInt(0, FirstName.Length)];
        }

        /// <summary>
        /// 生成带有生僻字部分的姓名
        /// </summary>
        /// <returns></returns>
        public string GenerateOdd()
        {
            return string.Empty;
        }
    }
}
