using System.Text;

namespace Bing.Helpers
{
    /// <summary>
    /// 字符串操作 - 字符串生成器
    /// </summary>
    public partial class Str
    {
        /// <summary>
        /// 字符串生成器
        /// </summary>
        private StringBuilder Builder { get; set; }

        /// <summary>
        /// 字符串长度
        /// </summary>
        public int Length => Builder.Length;

        /// <summary>
        /// 初始化一个<see cref="Str"/>类型的实例
        /// </summary>
        public Str() => Builder = new StringBuilder();

        /// <summary>
        /// 追加内容
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="value">值</param>
        public Str Append<T>(T value)
        {
            Builder.Append(value);
            return this;
        }

        /// <summary>
        /// 追加内容
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="args">参数</param>
        public Str Append(string value, params object[] args)
        {
            if (args == null)
                args = new object[] { string.Empty };
            if (args.Length == 0)
                Builder.Append(value);
            else
                Builder.AppendFormat(value, args);
            return this;
        }

        /// <summary>
        /// 追加内容并换行
        /// </summary>
        public Str AppendLine()
        {
            Builder.AppendLine();
            return this;
        }

        /// <summary>
        /// 追加内容并换行
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="value">值</param>
        public Str AppendLine<T>(T value)
        {
            Append(value);
            AppendLine();
            return this;
        }

        /// <summary>
        /// 追加内容并换行
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="args">参数</param>
        public Str AppendLine(string value, params object[] args)
        {
            Append(value, args);
            Builder.AppendLine();
            return this;
        }

        /// <summary>
        /// 替换内容
        /// </summary>
        /// <param name="value">值</param>
        public Str Replace(string value)
        {
            Builder.Clear();
            Builder.Append(value);
            return this;
        }

        /// <summary>
        /// 移除末尾字符串
        /// </summary>
        /// <param name="end">末尾字符串</param>
        public Str RemoveEnd(string end)
        {
            string result = Builder.ToString();
            if (!result.EndsWith(end))
                return this;
            Builder = new StringBuilder(result.TrimEnd(end.ToCharArray()));
            return this;
        }

        /// <summary>
        /// 清空字符串
        /// </summary>
        public Str Clear()
        {
            Builder = Builder.Clear();
            return this;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString() => Builder.ToString();
    }
}
