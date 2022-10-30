using System.Collections;

namespace Bing.Collections;

/// <summary>
/// 类型列表
/// </summary>
public class TypeList : TypeList<object>, ITypeList
{
}

/// <summary>
/// 类型列表
/// </summary>
/// <typeparam name="TBaseType">泛型基类型</typeparam>
public class TypeList<TBaseType> : ITypeList<TBaseType>
{
    /// <summary>
    /// 类型列表
    /// </summary>
    private readonly List<Type> _typeList;

    /// <summary>
    /// 计数
    /// </summary>
    public int Count => _typeList.Count;

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// 获取或设置类型
    /// </summary>
    /// <param name="index">索引值</param>
    public Type this[int index]
    {
        get => _typeList[index];
        set
        {
            CheckType(value);
            _typeList[index] = value;
        }
    }

    /// <summary>
    /// 初始化一个<see cref="TypeList{TBaseType}"/>类型的实例
    /// </summary>
    public TypeList() => _typeList = new List<Type>();

    /// <summary>
    /// 添加
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    public void Add<T>() where T : TBaseType => _typeList.Add(typeof(T));

    /// <summary>
    /// 尝试添加
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    public bool TryAdd<T>() where T : TBaseType
    {
        if (Contains<T>())
            return false;
        Add<T>();
        return true;
    }

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="item">类型</param>
    public void Add(Type item)
    {
        CheckType(item);
        _typeList.Add(item);
    }

    /// <summary>
    /// 插入
    /// </summary>
    /// <param name="index">索引值</param>
    /// <param name="item">类型</param>
    public void Insert(int index, Type item)
    {
        CheckType(item);
        _typeList.Insert(index, item);
    }

    /// <summary>
    /// 获取指定类型索引值
    /// </summary>
    /// <param name="item">类型</param>
    public int IndexOf(Type item) => _typeList.IndexOf(item);

    /// <summary>
    /// 包含
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    public bool Contains<T>() where T : TBaseType => Contains(typeof(T));

    /// <summary>
    /// 包含
    /// </summary>
    /// <param name="item">类型</param>
    /// <returns></returns>
    public bool Contains(Type item) => _typeList.Contains(item);

    /// <summary>
    /// 移除
    /// </summary>
    /// <typeparam name="T">泛型类型</typeparam>
    public void Remove<T>() where T : TBaseType => _typeList.Remove(typeof(T));

    /// <summary>
    /// 移除
    /// </summary>
    /// <param name="item">类型</param>
    public bool Remove(Type item) => _typeList.Remove(item);

    /// <summary>
    /// 移除指定索引的项
    /// </summary>
    /// <param name="index">索引</param>
    public void RemoveAt(int index) => _typeList.RemoveAt(index);

    /// <summary>
    /// 清空
    /// </summary>
    public void Clear() => _typeList.Clear();

    /// <summary>
    /// 复制到指定数组中
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="arrayIndex">数组索引</param>
    public void CopyTo(Type[] array, int arrayIndex) => _typeList.CopyTo(array, arrayIndex);

    /// <summary>
    /// 获取迭代器集合
    /// </summary>
    public IEnumerator<Type> GetEnumerator() => _typeList.GetEnumerator();

    /// <summary>
    /// 获取迭代器
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => _typeList.GetEnumerator();

    /// <summary>
    /// 检查类型
    /// </summary>
    /// <param name="item">类型</param>
    private static void CheckType(Type item)
    {
        if (!typeof(TBaseType).GetTypeInfo().IsAssignableFrom(item))
            throw new ArgumentException(
                $"Given type ({item.AssemblyQualifiedName}) should be instance of {typeof(TBaseType).AssemblyQualifiedName} ",
                nameof(item));
    }
}
