using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.MockData.Core;

namespace Bing.MockData.Generators.Address
{
    /// <summary>
    /// 地址生成器
    /// </summary>
    public class ChineseAddressRandomGenerator:RandomGeneratorBase,IRandomGenerator
    {
        /// <summary>
        /// 实例
        /// </summary>
        public static readonly ChineseAddressRandomGenerator Instance = new ChineseAddressRandomGenerator();

        /// <summary>
        /// 初始化一个<see cref="ChineseAddressRandomGenerator"/>类型的实例
        /// </summary>
        private ChineseAddressRandomGenerator() { }

        /// <summary>
        /// 生成地址
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            return BatchGenerate(1).First();
        }

        /// <summary>
        /// 批量生成地址
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        public override List<string> BatchGenerate(int maxLength)
        {
            List<string> list=new List<string>();
            for (int i = 0; i < maxLength; i++)
            {
                list.Add(GetAddress());
            }

            return list;
        }

        /// <summary>
        /// 随机生成一个大区
        /// </summary>
        /// <returns></returns>
        public string GenerateRegion()
        {
            return ChineseAreaList.RegionList[Builder.GenerateInt(0, ChineseAreaList.RegionList.Count)];
        }

        /// <summary>
        /// 获取地址
        /// </summary>
        /// <returns></returns>
        private string GetAddress()
        {
            StringBuilder sb=new StringBuilder();
            sb.Append(GetProvinceAndCity());
            sb.Append(Builder.GenerateRandomLengthChinese(2, 4) + "路");
            sb.Append(Builder.GenerateInt(1, 8001) + "号");
            sb.Append(Builder.GenerateRandomLengthChinese(2, 4) + "小区");
            sb.Append(Builder.GenerateInt(1, 21) + "单元");
            sb.Append(Builder.GenerateInt(101, 2501) + "室");
            return sb.ToString();
        }

        /// <summary>
        /// 获取省份城市
        /// </summary>
        /// <returns></returns>
        private string GetProvinceAndCity()
        {
            return ChineseAreaList.ProvinceCityList[Builder.GenerateInt(0, ChineseAreaList.ProvinceCityList.Count)];
        }
    }
}
