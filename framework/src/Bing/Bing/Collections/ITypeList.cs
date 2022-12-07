namespace Bing.Collections;

/// <summary>
/// 类型列表
/// </summary>
public interface ITypeList : ITypeList<object>
{
}

/// <summary>
/// 类型列表
/// </summary>
/// <typeparam name="TBaseType">泛型基类型</typeparam>
public interface ITypeList<in TBaseType> : IList<Type>
{
    /// <summary>
    /// 添加
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    void Add<T>() where T : TBaseType;

    /// <summary>
    /// 尝试添加
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    bool TryAdd<T>() where T : TBaseType;

    /// <summary>
    /// 包含
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    bool Contains<T>() where T : TBaseType;

    /// <summary>
    /// 移除
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    void Remove<T>() where T : TBaseType;
}
