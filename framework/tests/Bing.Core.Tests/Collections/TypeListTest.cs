using Bing.Collections;
using Shouldly;

namespace Bing.Tests.Collections;

/// <summary>
/// TypeList 类型列表 测试
/// </summary>
public class TypeListTest
{
    // ==================== 辅助接口/类 ====================

    private interface IAnimal { }
    private class Dog : IAnimal { }
    private class Cat : IAnimal { }
    private class Fish : IAnimal { }

    // ==================== Count / IsReadOnly ====================

    /// <summary>
    /// 测试目的：新建空列表，Count 应为 0，IsReadOnly 应为 false。
    /// </summary>
    [Fact]
    public void Count_EmptyList_IsZero()
    {
        // Arrange & Act
        var list = new TypeList<IAnimal>();

        // Assert
        list.Count.ShouldBe(0);
        list.IsReadOnly.ShouldBeFalse();
    }

    // ==================== Add (泛型) ====================

    /// <summary>
    /// 测试目的：通过泛型 Add&lt;T&gt; 添加合法类型后，Count 应增加，Contains&lt;T&gt; 应返回 true。
    /// </summary>
    [Fact]
    public void Add_Generic_ValidType_IncreasesCountAndContains()
    {
        // Arrange
        var list = new TypeList<IAnimal>();

        // Act
        list.Add<Dog>();

        // Assert
        list.Count.ShouldBe(1);
        list.Contains<Dog>().ShouldBeTrue();
    }

    // ==================== Add (Type) ====================

    /// <summary>
    /// 测试目的：通过 Add(Type) 添加合法类型，与泛型 Add 效果相同。
    /// </summary>
    [Fact]
    public void Add_Type_ValidType_Works()
    {
        // Arrange
        var list = new TypeList<IAnimal>();

        // Act
        list.Add(typeof(Cat));

        // Assert
        list.Contains(typeof(Cat)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：通过 Add(Type) 添加不兼容类型，应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void Add_Type_IncompatibleType_ThrowsArgumentException()
    {
        // Arrange
        var list = new TypeList<IAnimal>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => list.Add(typeof(string)));
    }

    // ==================== TryAdd ====================

    /// <summary>
    /// 测试目的：TryAdd 新类型应返回 true 并添加成功。
    /// </summary>
    [Fact]
    public void TryAdd_NewType_ReturnsTrueAndAdds()
    {
        // Arrange
        var list = new TypeList<IAnimal>();

        // Act
        var result = list.TryAdd<Dog>();

        // Assert
        result.ShouldBeTrue();
        list.Contains<Dog>().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：TryAdd 已存在类型应返回 false，不重复添加。
    /// </summary>
    [Fact]
    public void TryAdd_DuplicateType_ReturnsFalseAndDoesNotDuplicate()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();

        // Act
        var result = list.TryAdd<Dog>();

        // Assert
        result.ShouldBeFalse();
        list.Count.ShouldBe(1);
    }

    // ==================== Contains ====================

    /// <summary>
    /// 测试目的：未添加的类型，Contains&lt;T&gt; 应返回 false。
    /// </summary>
    [Fact]
    public void Contains_NotAdded_ReturnsFalse()
    {
        // Arrange
        var list = new TypeList<IAnimal>();

        // Act & Assert
        list.Contains<Dog>().ShouldBeFalse();
        list.Contains(typeof(Dog)).ShouldBeFalse();
    }

    // ==================== Remove ====================

    /// <summary>
    /// 测试目的：泛型 Remove&lt;T&gt; 应从列表中删除该类型。
    /// </summary>
    [Fact]
    public void Remove_Generic_RemovesType()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();

        // Act
        list.Remove<Dog>();

        // Assert
        list.Contains<Dog>().ShouldBeFalse();
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：Remove(Type) 删除已添加类型应返回 true；删除不存在类型应返回 false。
    /// </summary>
    [Fact]
    public void Remove_Type_ExistingReturnsTrue_MissingReturnsFalse()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();

        // Act & Assert
        list.Remove(typeof(Dog)).ShouldBeTrue();
        list.Remove(typeof(Cat)).ShouldBeFalse();
    }

    // ==================== RemoveAt ====================

    /// <summary>
    /// 测试目的：RemoveAt(0) 应移除第一个元素。
    /// </summary>
    [Fact]
    public void RemoveAt_Index_RemovesCorrectElement()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();
        list.Add<Cat>();

        // Act
        list.RemoveAt(0);

        // Assert
        list.Count.ShouldBe(1);
        list.Contains<Dog>().ShouldBeFalse();
        list.Contains<Cat>().ShouldBeTrue();
    }

    // ==================== Clear ====================

    /// <summary>
    /// 测试目的：Clear 后 Count 应变为 0，所有元素都消失。
    /// </summary>
    [Fact]
    public void Clear_RemovesAllTypes()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();
        list.Add<Cat>();

        // Act
        list.Clear();

        // Assert
        list.Count.ShouldBe(0);
        list.Contains<Dog>().ShouldBeFalse();
    }

    // ==================== IndexOf ====================

    /// <summary>
    /// 测试目的：IndexOf 返回正确的索引位置；未找到返回 -1。
    /// </summary>
    [Fact]
    public void IndexOf_ReturnsCorrectIndex()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();
        list.Add<Cat>();

        // Act & Assert
        list.IndexOf(typeof(Dog)).ShouldBe(0);
        list.IndexOf(typeof(Cat)).ShouldBe(1);
        list.IndexOf(typeof(Fish)).ShouldBe(-1);
    }

    // ==================== Indexer ====================

    /// <summary>
    /// 测试目的：通过索引器可以正确读取类型。
    /// </summary>
    [Fact]
    public void Indexer_Get_ReturnsCorrectType()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();
        list.Add<Cat>();

        // Act & Assert
        list[0].ShouldBe(typeof(Dog));
        list[1].ShouldBe(typeof(Cat));
    }

    /// <summary>
    /// 测试目的：通过索引器设置合法类型应成功；设置不兼容类型应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void Indexer_Set_ValidType_Succeeds_InvalidType_Throws()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();

        // Act: 用兼容类型替换
        list[0] = typeof(Cat);
        list[0].ShouldBe(typeof(Cat));

        // Assert: 不兼容类型抛出
        Should.Throw<ArgumentException>(() => list[0] = typeof(int));
    }

    // ==================== Insert ====================

    /// <summary>
    /// 测试目的：Insert 在指定位置插入类型，后续元素向后移动。
    /// </summary>
    [Fact]
    public void Insert_AtIndex_ShiftsElements()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Cat>();

        // Act
        list.Insert(0, typeof(Dog));

        // Assert
        list[0].ShouldBe(typeof(Dog));
        list[1].ShouldBe(typeof(Cat));
    }

    /// <summary>
    /// 测试目的：Insert 不兼容类型应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void Insert_IncompatibleType_ThrowsArgumentException()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => list.Insert(0, typeof(string)));
    }

    // ==================== CopyTo ====================

    /// <summary>
    /// 测试目的：CopyTo 将所有元素复制到目标数组。
    /// </summary>
    [Fact]
    public void CopyTo_CopiesAllTypesToArray()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();
        list.Add<Cat>();
        var array = new Type[2];

        // Act
        list.CopyTo(array, 0);

        // Assert
        array[0].ShouldBe(typeof(Dog));
        array[1].ShouldBe(typeof(Cat));
    }

    // ==================== IEnumerable ====================

    /// <summary>
    /// 测试目的：通过 foreach 可以枚举所有类型，顺序与添加顺序一致。
    /// </summary>
    [Fact]
    public void GetEnumerator_EnumeratesAllTypesInOrder()
    {
        // Arrange
        var list = new TypeList<IAnimal>();
        list.Add<Dog>();
        list.Add<Cat>();
        list.Add<Fish>();

        // Act
        var result = list.ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].ShouldBe(typeof(Dog));
        result[1].ShouldBe(typeof(Cat));
        result[2].ShouldBe(typeof(Fish));
    }

    // ==================== 非泛型 TypeList ====================

    /// <summary>
    /// 测试目的：非泛型 TypeList 继承自 TypeList&lt;object&gt;，可以添加任意类型。
    /// </summary>
    [Fact]
    public void TypeList_NonGeneric_AcceptsAnyType()
    {
        // Arrange & Act
        var list = new TypeList();
        list.Add(typeof(string));
        list.Add(typeof(int));
        list.Add(typeof(Dog));

        // Assert
        list.Count.ShouldBe(3);
        list.Contains(typeof(string)).ShouldBeTrue();
    }
}
