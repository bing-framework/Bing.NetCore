namespace System.Data
{
    /// <summary>
    /// 数据列集合(<see cref="DataColumnCollection"/>) 扩展
    /// </summary>
    public static class DataColumnCollectionExtensions
    {
        /// <summary>
        /// 添加集合
        /// </summary>
        /// <param name="this">DataColumnCollection</param>
        /// <param name="columns">列名数组</param>
        public static void AddRange(this DataColumnCollection @this, params string[] columns)
        {
            foreach (var column in columns) 
                @this.Add(column);
        }
    }
}
