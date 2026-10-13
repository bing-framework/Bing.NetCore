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

    /// <inheritdoc />
    public int Count => _typeList.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
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
    /// 初始化一个 <see cref="TypeList{TBaseType}"/> 类型的实例。
    /// </summary>
    public TypeList() => _typeList = new List<Type>();

    /// <inheritdoc />
    public void Add<T>() where T : TBaseType => _typeList.Add(typeof(T));

    /// <inheritdoc />
    public bool TryAdd<T>() where T : TBaseType
    {
        if (Contains<T>())
            return false;
        Add<T>();
        return true;
    }

    /// <inheritdoc />
    public void Add(Type item)
    {
        CheckType(item);
        _typeList.Add(item);
    }

    /// <inheritdoc />
    public void Insert(int index, Type item)
    {
        CheckType(item);
        _typeList.Insert(index, item);
    }

    /// <inheritdoc />
    public int IndexOf(Type item) => _typeList.IndexOf(item);

    /// <inheritdoc />
    public bool Contains<T>() where T : TBaseType => Contains(typeof(T));

    /// <inheritdoc />
    public bool Contains(Type item) => _typeList.Contains(item);

    /// <inheritdoc />
    public void Remove<T>() where T : TBaseType => _typeList.Remove(typeof(T));

    /// <inheritdoc />
    public bool Remove(Type item) => _typeList.Remove(item);

    /// <inheritdoc />
    public void RemoveAt(int index) => _typeList.RemoveAt(index);

    /// <inheritdoc />
    public void Clear() => _typeList.Clear();

    /// <inheritdoc />
    public void CopyTo(Type[] array, int arrayIndex) => _typeList.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<Type> GetEnumerator() => _typeList.GetEnumerator();

    /// <inheritdoc />
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
