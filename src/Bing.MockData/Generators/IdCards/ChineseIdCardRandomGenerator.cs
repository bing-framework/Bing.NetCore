using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Core;
using Bing.MockData.Generators.Address;

namespace Bing.MockData.Generators.IdCards
{
    /// <summary>
    /// 身份证号码生成器
    /// </summary>
    public class ChineseIdCardRandomGenerator: RandomGeneratorBase, IRandomGenerator
    {
        /**
         * 身份证号码
         * 1、号码的结构
         * 公民身份号码是特征组合码，由十七位数字本体码和一位校验码组成。排列顺序从左至右依次为：六位数字地址码，
         * 八位数字出生日期码，三位数字顺序码和一位数字校验码。
         * 2、地址码(前六位数）
         * 表示编码对象常住户口所在县(市、旗、区)的行政区划代码，按GB/T2260的规定执行。
         * 3、出生日期码（第七位至十四位）
         * 表示编码对象出生的年、月、日，按GB/T7408的规定执行，年、月、日代码之间不用分隔符。
         * 4、顺序码（第十五位至十七位）
         * 表示在同一地址码所标识的区域范围内，对同年、同月、同日出生的人编定的顺序号，
         * 顺序码的奇数分配给男性，偶数分配给女性。
         * 5、校验码（第十八位数）
         * （1）十七位数字本体码加权求和公式 S = Sum(Ai * Wi), i = 0, ... , 16 ，先对前17位数字的权求和
         * Ai:表示第i位置上的身份证号码数字值 Wi:表示第i位置上的加权因子 Wi: 7 9 10 5 8 4 2 1 6 3 7 9 10 5 8 4
         * 2 （2）计算模 Y = mod(S, 11) （3）通过模得到对应的校验码 Y: 0 1 2 3 4 5 6 7 8 9 10 校验码: 1 0
         * X 9 8 7 6 5 4 3 2
         */

        /// <summary>
        /// 实例
        /// </summary>
        public static readonly ChineseIdCardRandomGenerator Instance = new ChineseIdCardRandomGenerator();

        /// <summary>
        /// 校验码
        /// </summary>
        private static readonly char[] VerifyCode = { '1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };

        /// <summary>
        /// 校验码权值
        /// </summary>
        private static readonly int[] VerifyCodeWeight = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };

        /// <summary>
        /// 初始化一个<see cref="ChineseIdCardRandomGenerator"/>类型的实例
        /// </summary>
        private ChineseIdCardRandomGenerator() { }

        /// <summary>
        /// 生成身份证号码
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            return BatchGenerate(1).First();
        }

        /// <summary>
        /// 批量生成身份证号码
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        public override List<string> BatchGenerate(int maxLength)
        {
            List<string> list = new List<string>();
            for (int i = 0; i < maxLength; i++)
            {
                list.Add(GetIdCard());
            }

            return list;
        }        

        /// <summary>
        /// 生成签发机关：XXX公安局/XX区分局
        /// </summary>
        /// <returns></returns>
        public string GenerateIssueOrg()
        {
            return ChineseAreaList.CityNameList[Builder.GenerateInt(0, ChineseAreaList.CityNameList.Count)] + "公安局某某分局";
        }

        /// <summary>
        /// 生成有效期限
        /// </summary>
        /// <returns></returns>
        public string GenerateValidPeriod()
        {
            string formater = "yyyyMMdd";
            var beginDate = Builder.GenerateDate();            
            var endDate = beginDate.AddYears(20);
            return $"{beginDate.ToString(formater)}-{endDate.ToString(formater)}";
        }

        /// <summary>
        /// 获取身份证号码
        /// </summary>
        /// <returns></returns>
        private string GetIdCard()
        {
            var areaCode = ChineseAreaList.AreaCodeDict[Builder.GenerateInt(0,ChineseAreaList.AreaCodeDict.Count)].Key +
                           Builder.GenerateInt(9999).ToString().PadLeft(4, '0');
            var birthday = Builder.GenerateDate().ToString("yyyyMMdd");
            var randomCode = Builder.GenerateNumber(3);
            var pre = areaCode + birthday + randomCode;
            var verifyCode = CalculateVerifyCode(pre);
            return pre + verifyCode;
        }

        /// <summary>
        /// 计算校验码
        /// </summary>
        /// <param name="source">身份证号码</param>
        /// <returns></returns>
        private char CalculateVerifyCode(string source)
        {
            /*
             * <li>校验码（第十八位数）：<br/>
             * <ul>
             * <li>十七位数字本体码加权求和公式 S = Sum(Ai * Wi), i = 0...16 ，先对前17位数字的权求和；
             * Ai:表示第i位置上的身份证号码数字值 Wi:表示第i位置上的加权因子 Wi: 7 9 10 5 8 4 2 1 6 3 7 9 10 5 8 4
             * 2；</li>
             * <li>计算模 Y = mod(S, 11)</li>
             * <li>通过模得到对应的校验码 Y: 0 1 2 3 4 5 6 7 8 9 10 校验码: 1 0 X 9 8 7 6 5 4 3 2</li>
             * </ul>
             */
            int sum = 0;
            for (int i = 0; i < 18 - 1; i++)
            {
                char ch = source[i];
                sum += ((int)(ch - '0')) * VerifyCodeWeight[i];
            }
            return VerifyCode[sum % 11];
        }
    }
}
