using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 表示可被框架替换的服务作用域工厂抽象。
/// </summary>
/// <remarks>该接口复用 <see cref="IServiceScopeFactory"/> 的创建契约，用作框架内部的可替换服务标识。</remarks>
public interface IHybridServiceScopeFactory : IServiceScopeFactory
{
}
