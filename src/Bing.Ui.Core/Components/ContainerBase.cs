using System;
using System.IO;
using Bing.Ui.Components.Internal;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 容器基类
    /// </summary>
    /// <typeparam name="TWrapper">容器包装器类型</typeparam>
    public abstract class ContainerBase<TWrapper> : ComponentBase, IContainer<TWrapper>, IRenderEnd
        where TWrapper : IDisposable
    {
        /// <summary>
        /// 流写入器
        /// </summary>
        public TextWriter Writer { get; set; }

        /// <summary>
        /// 初始化一个<see cref="ContainerBase{TWrapper}"/>类型的实例
        /// </summary>
        /// <param name="writer">流写入器</param>
        protected ContainerBase(TextWriter writer)
        {
            Writer = writer;
        }

        /// <summary>
        /// 准备渲染器
        /// </summary>
        public TWrapper Begin()
        {
            if (Writer == null)
            {
                throw new ArgumentNullException("TextWriter未设置");
            }
            Render.RenderStartTag(Writer);
            return GetWrapper();
        }

        /// <summary>
        /// 获取容器包装器
        /// </summary>
        protected abstract TWrapper GetWrapper();

        /// <summary>
        /// 容器渲染结束
        /// </summary>
        public void End()
        {
            Render.RenderEndTag(Writer);
        }
    }
}
