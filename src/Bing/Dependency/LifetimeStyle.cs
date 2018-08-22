namespace Bing.Dependency
{
    /// <summary>
    /// 表示依赖注入的对象声明周期
    /// </summary>
    public enum LifetimeStyle
    {
        /// <summary>
        /// 实时模式，每次获取都创建不同对象
        /// </summary>
        Transient,
        /// <summary>
        /// 局部模式，同一生命周期获得相同对象，不同生命周期获取不同对象（PerRequest）
        /// </summary>
        Scoped,
        /// <summary>
        /// 单例模式，在第一获取实例时创建，之后都获取相同对象
        /// </summary>
        Singleton
    }
}
