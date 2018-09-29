using System;
using System.Text;

namespace Bing.Offices.Excels.Internal
{
    /// <summary>
    /// 工作簿操作类
    /// </summary>
    public class WorkbookUtil
    {
        /// <summary>
        /// 创建安全的工作表名称
        /// </summary>
        /// <param name="nameProposal">名称</param>
        /// <returns></returns>
        public static string CreateSafeSheetName(string nameProposal)
        {
            return CreateSafeSheetName(nameProposal, ' ');
        }

        /// <summary>
        /// 创建安全的工作表名称
        /// </summary>
        /// <param name="nameProposal">名称</param>
        /// <param name="replaceChar">替换字符</param>
        /// <returns></returns>
        public static string CreateSafeSheetName(string nameProposal, char replaceChar)
        {
            if (nameProposal == null)
            {
                return "null";
            }

            if (nameProposal.Length < 1)
            {
                return "empty";
            }

            int length = Math.Min(31, nameProposal.Length);
            string shortenName = nameProposal.Substring(0, length);
            StringBuilder result = new StringBuilder(shortenName);
            for (int i = 0; i < length; i++)
            {
                char ch = result[i];
                switch (ch)
                {
                    case '\u0000':
                    case '\u0003':
                    case ':':
                    case '/':
                    case '\\':
                    case '?':
                    case '*':
                    case ']':
                    case '[':
                        result[i] = replaceChar;
                        break;
                    case '\'':
                        if (i == 0 || i == length - 1)
                        {
                            result[i] = replaceChar;
                        }
                        break;
                    default:
                        // all other chars OK
                        break;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// 验证工作表名称
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        public static void ValidateSheetName(string sheetName)
        {
            if (sheetName == null)
            {
                throw new ArgumentException("sheetName must not be null");
            }

            int len = sheetName.Length;
            if (len < 1 || len > 31)
            {
                throw new ArgumentException($"sheetName '{sheetName}' is invalid - character count MUST be greater than or equal to 1 and less than or eqaul to 31");
            }

            for (int i = 0; i < len; i++)
            {
                char ch = sheetName[i];
                switch (ch)
                {
                    case '/':
                    case '\\':
                    case '?':
                    case '*':
                    case ']':
                    case '[':
                    case ':':
                        break;
                    default:
                        // all other chars OK
                        continue;
                }
                throw new ArgumentException($"Invalid char ({ch}) found at index ({i}) in sheet name '{sheetName}'");
            }

            if (sheetName[0] == '\'' || sheetName[len - 1] == '\'')
            {
                throw new ArgumentException($"Invalid sheet name '{sheetName}'. Sheet names must not begin or end with (').");
            }
        }
    }
}
