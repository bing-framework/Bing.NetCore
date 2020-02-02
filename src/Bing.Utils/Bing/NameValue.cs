namespace Bing
{
    /// <summary>
    /// 名称 - 值
    /// </summary>
    public class NameValue : NameValue<string>
    {
        /// <summary>
        /// 初始化一个<see cref="NameValue"/>类型的实例
        /// </summary>
        public NameValue() { }

        /// <summary>
        /// 初始化一个<see cref="NameValue"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public NameValue(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    /// <summary>
    /// 名称 - 值
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public class NameValue<T>
    {
        /// <summary>
        /// 初始化一个<see cref="NameValue{T}"/>类型的实例
        /// </summary>
        public NameValue() { }

        /// <summary>
        /// 初始化一个<see cref="NameValue{T}"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public NameValue(string name, T value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public T Value { get; set; }
    }
}
