using System.Collections.ObjectModel;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

public sealed partial class BingHotPluginHost<TStartupModule> where TStartupModule : BingModule
{
    /// <summary>
    /// 保存一代插件的容器、上下文与活动调用状态。
    /// </summary>
    private sealed class Generation
    {
        /// <summary>
        /// 保护活动调用计数。
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        /// 旧代全部调用退出时完成。
        /// </summary>
        private readonly TaskCompletionSource<bool> _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 当前活动调用数。
        /// </summary>
        private int _active;

        /// <summary>
        /// 是否已禁止新调用。
        /// </summary>
        private bool _retired;

        /// <summary>
        /// 是否正在关闭模块和容器。
        /// </summary>
        private bool _closing;

        /// <summary>
        /// 初始化一个 <see cref="Generation" /> 类型的实例。
        /// </summary>
        /// <param name="id">插件代标识。</param>
        /// <param name="context">程序集加载上下文。</param>
        /// <param name="provider">本代服务容器。</param>
        /// <param name="plugins">插件信息快照。</param>
        internal Generation(Guid id, AssemblyLoadContext context, ServiceProvider provider,
            IReadOnlyList<BingHotPluginInfo> plugins)
        {
            Id = id;
            Context = context;
            Provider = provider;
            Plugins = plugins;
        }

        /// <summary>
        /// 获取插件代标识。
        /// </summary>
        internal Guid Id { get; }

        /// <summary>
        /// 获取程序集加载上下文。
        /// </summary>
        internal AssemblyLoadContext Context { get; }

        /// <summary>
        /// 获取本代服务容器。
        /// </summary>
        internal ServiceProvider Provider { get; }

        /// <summary>
        /// 获取插件信息快照。
        /// </summary>
        internal IReadOnlyList<BingHotPluginInfo> Plugins { get; }

        /// <summary>
        /// 获取插件代状态快照。
        /// </summary>
        /// <returns>插件代状态快照。</returns>
        /// <remarks>快照不持有插件程序集引用。</remarks>
        internal BingHotPluginGenerationStatus Snapshot()
        {
            lock (_gate)
            {
                var state = _closing ? BingHotPluginGenerationState.Closing :
                    _retired ? BingHotPluginGenerationState.Draining : BingHotPluginGenerationState.Active;
                return new BingHotPluginGenerationStatus(Id, Plugins, _active, state);
            }
        }

        /// <summary>
        /// 登记当前调用。
        /// </summary>
        internal void Acquire()
        {
            lock (_gate)
            {
                if (_retired) throw new InvalidOperationException("插件代已停止接收新调用。");
                _active++;
            }
        }

        /// <summary>
        /// 释放当前调用。
        /// </summary>
        internal void Release()
        {
            lock (_gate)
            {
                _active--;
                if (_retired && _active == 0) _idle.TrySetResult(true);
            }
        }

        /// <summary>
        /// 停止接收新调用。
        /// </summary>
        internal void Retire()
        {
            lock (_gate)
            {
                _retired = true;
                if (_active == 0) _idle.TrySetResult(true);
            }
        }

        /// <summary>
        /// 等待全部已开始的调用退出。
        /// </summary>
        internal Task WaitForIdleAsync() => _idle.Task;

        /// <summary>
        /// 标记插件代开始关闭。
        /// </summary>
        internal void MarkClosing()
        {
            lock (_gate) _closing = true;
        }

        /// <summary>
        /// 关闭模块、释放服务并请求卸载程序集。
        /// </summary>
        internal async Task CloseAsync()
        {
            var errors = new List<Exception>();
            try { await Provider.ShutdownBingAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
            try { await Provider.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
            Context.Unload();
            if (errors.Count > 0)
                throw new AggregateException("插件代关闭失败；已尝试释放容器并请求卸载程序集。", errors);
        }
    }
}
