using System;

namespace Bing.Core.Data
{
    /// <summary>
    /// 数据处理委托
    /// </summary>
    /// <param name="o">引发对象</param>
    /// <param name="e">数据事件参数</param>
    public delegate void DataHandler(object o, DataEventArgs e);

    /// <summary>
    /// 数据事件参数
    /// </summary>
    public class DataEventArgs : EventArgs
    {
        /// <summary>
        /// 数据
        /// </summary>
        public object Data { get; }

        /// <summary>
        /// 初始化一个<see cref="DataEventArgs"/>类型的实例
        /// </summary>
        /// <param name="data">数据</param>
        public DataEventArgs(object data = null) => Data = data;
    }
}
