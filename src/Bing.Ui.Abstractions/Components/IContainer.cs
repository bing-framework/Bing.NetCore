using System;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 容器
    /// </summary>
    public interface IContainer:IOption
    {
    }

    /// <summary>
    /// 容器
    /// </summary>
    /// <typeparam name="TWrapper">包装器类型</typeparam>
    public interface IContainer<out TWrapper> : IContainer where TWrapper : IDisposable
    {
        /// <summary>
        /// 准备渲染器
        /// </summary>
        /// <returns></returns>
        TWrapper Begin();
    }
}
