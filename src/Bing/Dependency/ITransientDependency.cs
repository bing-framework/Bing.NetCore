namespace Bing.Dependency
{
    /// <summary>
    /// 实现此接口的类型将自动注册为<see cref="LifetimeStyle.Transient"/>模式
    /// </summary>
    [IgnoreDependency]
    public interface ITransientDependency : IDependency
    {
    }
}
