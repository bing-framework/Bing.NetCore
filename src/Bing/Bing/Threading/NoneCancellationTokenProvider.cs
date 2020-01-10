using System.Threading;
using Bing.Dependency;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Threading
{
    /// <summary>
    /// 空异步任务取消令牌提供程序
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    public class NoneCancellationTokenProvider : ICancellationTokenProvider
    {
        /// <summary>
        /// 空异步任务取消令牌提供程序实例
        /// </summary>
        public static readonly ICancellationTokenProvider Instance = new NoneCancellationTokenProvider();

        /// <summary>
        /// 异步任务取消令牌
        /// </summary>
        public CancellationToken Token { get; } = CancellationToken.None;
    }
}
