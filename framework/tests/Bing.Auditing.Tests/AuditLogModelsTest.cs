using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Bing.Auditing.Tests;

// =========================================================================
//  AuditLogActionInfo Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 AuditLogActionInfo 属性读写行为。
/// </summary>
public class AuditLogActionInfoTest
{
    /// <summary>
    /// 测试目的：所有属性可正常读写，默认值均为 null/0。
    /// </summary>
    [Fact]
    public void Properties_DefaultValues_ShouldBeNullOrZero()
    {
        var info = new AuditLogActionInfo();
        info.ServiceName.ShouldBeNull();
        info.MethodName.ShouldBeNull();
        info.Parameters.ShouldBeNull();
        info.ExecutionTime.ShouldBe(default);
        info.ExecutionDuration.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：为所有属性赋值后可正常读取，值不丢失。
    /// </summary>
    [Fact]
    public void Properties_SetValues_ShouldBeRetrievable()
    {
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var info = new AuditLogActionInfo
        {
            ServiceName = "UserAppService",
            MethodName = "CreateAsync",
            Parameters = "{ \"name\": \"Alice\" }",
            ExecutionTime = now,
            ExecutionDuration = 42
        };

        info.ServiceName.ShouldBe("UserAppService");
        info.MethodName.ShouldBe("CreateAsync");
        info.Parameters.ShouldBe("{ \"name\": \"Alice\" }");
        info.ExecutionTime.ShouldBe(now);
        info.ExecutionDuration.ShouldBe(42);
    }
}

// =========================================================================
//  AuditLogInfo Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 AuditLogInfo 构造初始化、属性读写及 ToString 输出。
/// </summary>
public class AuditLogInfoTest
{
    /// <summary>
    /// 测试目的：构造后各集合字段均应被初始化为空列表，不为 null。
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeAllCollections()
    {
        var info = new AuditLogInfo();
        info.Actions.ShouldNotBeNull();
        info.Actions.ShouldBeEmpty();
        info.Exceptions.ShouldNotBeNull();
        info.Exceptions.ShouldBeEmpty();
        info.EntityChanges.ShouldNotBeNull();
        info.EntityChanges.ShouldBeEmpty();
        info.Comments.ShouldNotBeNull();
        info.Comments.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：属性赋值后可正常读取。
    /// </summary>
    [Fact]
    public void Properties_SetValues_ShouldBeRetrievable()
    {
        var info = new AuditLogInfo
        {
            ApplicationName = "MyApp",
            UserId = "u-001",
            UserName = "Alice",
            TenantId = "t-001",
            TenantName = "Tenant1",
            HttpMethod = "POST",
            HttpStatusCode = 200,
            Url = "/api/users",
            ClientIpAddress = "127.0.0.1",
            CorrelationId = "corr-001",
            ExecutionDuration = 150
        };

        info.ApplicationName.ShouldBe("MyApp");
        info.UserId.ShouldBe("u-001");
        info.UserName.ShouldBe("Alice");
        info.TenantId.ShouldBe("t-001");
        info.TenantName.ShouldBe("Tenant1");
        info.HttpMethod.ShouldBe("POST");
        info.HttpStatusCode.ShouldBe(200);
        info.Url.ShouldBe("/api/users");
        info.ClientIpAddress.ShouldBe("127.0.0.1");
        info.CorrelationId.ShouldBe("corr-001");
        info.ExecutionDuration.ShouldBe(150);
    }

    /// <summary>
    /// 测试目的：ToString() 在无操作/异常/实体变更时不抛异常，且包含基本字段。
    /// </summary>
    [Fact]
    public void ToString_WithBasicFields_ShouldNotThrowAndContainUrl()
    {
        var info = new AuditLogInfo
        {
            HttpMethod = "GET",
            HttpStatusCode = 200,
            Url = "/api/health",
            UserName = "Alice",
            UserId = "u-001",
            ClientIpAddress = "192.168.1.1",
            ExecutionDuration = 10
        };

        var result = Should.NotThrow(() => info.ToString());
        result.ShouldContain("/api/health");
        result.ShouldContain("GET");
        result.ShouldContain("Alice");
    }

    /// <summary>
    /// 测试目的：ToString() 包含 Actions 时应输出 ServiceName.MethodName 行。
    /// </summary>
    [Fact]
    public void ToString_WithActions_ShouldContainServiceAndMethodName()
    {
        var info = new AuditLogInfo
        {
            HttpMethod = "POST",
            Url = "/api/orders"
        };
        info.Actions.Add(new AuditLogActionInfo
        {
            ServiceName = "OrderService",
            MethodName = "PlaceOrder",
            Parameters = "{}",
            ExecutionDuration = 20
        });

        var result = info.ToString();
        result.ShouldContain("OrderService");
        result.ShouldContain("PlaceOrder");
    }

    /// <summary>
    /// 测试目的：ToString() 包含 Exceptions 时应输出异常消息。
    /// </summary>
    [Fact]
    public void ToString_WithExceptions_ShouldContainExceptionMessage()
    {
        var info = new AuditLogInfo { HttpMethod = "DELETE", Url = "/api/users/1" };
        info.Exceptions.Add(new InvalidOperationException("Entity not found"));

        var result = info.ToString();
        result.ShouldContain("Entity not found");
    }

    /// <summary>
    /// 测试目的：HttpStatusCode 为 null 时 ToString() 应输出 "---"，不抛异常。
    /// </summary>
    [Fact]
    public void ToString_NullStatusCode_ShouldOutputPlaceholder()
    {
        var info = new AuditLogInfo { HttpMethod = "GET", Url = "/" };
        // HttpStatusCode defaults to null
        var result = Should.NotThrow(() => info.ToString());
        result.ShouldContain("---");
    }
}

// =========================================================================
//  EntityPropertyChangeInfo Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 EntityPropertyChangeInfo 属性读写。
/// </summary>
public class EntityPropertyChangeInfoTest
{
    /// <summary>
    /// 测试目的：属性赋值后可正常读取，默认均为 null。
    /// </summary>
    [Fact]
    public void Properties_SetAndGet_ShouldWork()
    {
        var change = new EntityPropertyChangeInfo
        {
            PropertyName = "Name",
            PropertyTypeFullName = "System.String",
            OriginalValue = "OldName",
            NewValue = "NewName"
        };

        change.PropertyName.ShouldBe("Name");
        change.PropertyTypeFullName.ShouldBe("System.String");
        change.OriginalValue.ShouldBe("OldName");
        change.NewValue.ShouldBe("NewName");
    }

    /// <summary>
    /// 测试目的：默认构造时所有属性为 null。
    /// </summary>
    [Fact]
    public void Constructor_DefaultValues_ShouldBeNull()
    {
        var change = new EntityPropertyChangeInfo();
        change.PropertyName.ShouldBeNull();
        change.PropertyTypeFullName.ShouldBeNull();
        change.OriginalValue.ShouldBeNull();
        change.NewValue.ShouldBeNull();
    }
}

// =========================================================================
//  EntityChangeInfo Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 EntityChangeInfo 属性读写及 Merge 逻辑。
/// </summary>
public class EntityChangeInfoTest
{
    private static EntityChangeInfo CreateWithChanges(params EntityPropertyChangeInfo[] props)
    {
        var e = new EntityChangeInfo
        {
            EntityId = "1",
            EntityTypeFullName = "Bing.Order",
            EntityTenantId = "t-001",
            PropertyChanges = new List<EntityPropertyChangeInfo>(props)
        };
        return e;
    }

    /// <summary>
    /// 测试目的：属性赋值后可正常读取。
    /// </summary>
    [Fact]
    public void Properties_SetAndGet_ShouldWork()
    {
        var now = new DateTime(2025, 1, 1);
        var e = new EntityChangeInfo
        {
            ChangeTime = now,
            EntityId = "42",
            EntityTypeFullName = "Bing.Domain.Order",
            EntityTenantId = "tenant-001",
            PropertyChanges = new List<EntityPropertyChangeInfo>()
        };

        e.ChangeTime.ShouldBe(now);
        e.EntityId.ShouldBe("42");
        e.EntityTypeFullName.ShouldBe("Bing.Domain.Order");
        e.EntityTenantId.ShouldBe("tenant-001");
        e.PropertyChanges.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：Merge 合并另一个变更时，新增属性应被添加。
    /// </summary>
    [Fact]
    public void Merge_WithNewProperty_ShouldAddToPropertyChanges()
    {
        // Arrange
        var target = CreateWithChanges(
            new EntityPropertyChangeInfo { PropertyName = "Name", OriginalValue = "A", NewValue = "B" });
        var source = CreateWithChanges(
            new EntityPropertyChangeInfo { PropertyName = "Age", OriginalValue = "20", NewValue = "21" });

        // Act
        target.Merge(source);

        // Assert
        target.PropertyChanges.Count.ShouldBe(2);
        target.PropertyChanges.ShouldContain(p => p.PropertyName == "Age");
    }

    /// <summary>
    /// 测试目的：Merge 合并同名属性时，应更新 NewValue 而不增加条目。
    /// </summary>
    [Fact]
    public void Merge_WithExistingProperty_ShouldUpdateNewValue()
    {
        // Arrange
        var target = CreateWithChanges(
            new EntityPropertyChangeInfo { PropertyName = "Name", OriginalValue = "A", NewValue = "B" });
        var source = CreateWithChanges(
            new EntityPropertyChangeInfo { PropertyName = "Name", OriginalValue = "B", NewValue = "C" });

        // Act
        target.Merge(source);

        // Assert
        target.PropertyChanges.Count.ShouldBe(1);
        target.PropertyChanges[0].NewValue.ShouldBe("C");
        target.PropertyChanges[0].OriginalValue.ShouldBe("A"); // 原始值不变
    }

    /// <summary>
    /// 测试目的：Merge 空属性列表时不抛异常，原集合保持不变。
    /// </summary>
    [Fact]
    public void Merge_EmptySource_ShouldNotChangeTarget()
    {
        var target = CreateWithChanges(
            new EntityPropertyChangeInfo { PropertyName = "Name", NewValue = "X" });
        var emptySource = new EntityChangeInfo { PropertyChanges = new List<EntityPropertyChangeInfo>() };

        target.Merge(emptySource);

        target.PropertyChanges.Count.ShouldBe(1);
    }
}

// =========================================================================
//  DisableAuditingAttribute Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 DisableAuditingAttribute 特性的声明元数据正确性。
/// </summary>
public class DisableAuditingAttributeTest
{
    /// <summary>
    /// 测试目的：DisableAuditingAttribute 应继承自 Attribute。
    /// </summary>
    [Fact]
    public void DisableAuditingAttribute_ShouldBeAttribute()
    {
        var attributeType = typeof(AuditLogInfo).Assembly.GetType("Bing.Auditing.DisableAuditingAttribute", throwOnError: true)!;
        var attr = Activator.CreateInstance(attributeType);
        attr.ShouldBeAssignableTo<Attribute>();
    }

    /// <summary>
    /// 测试目的：DisableAuditingAttribute 应标记 [Obsolete]。
    /// </summary>
    [Fact]
    public void DisableAuditingAttribute_ShouldBeMarkedObsolete()
    {
        var attributeType = typeof(AuditLogInfo).Assembly.GetType("Bing.Auditing.DisableAuditingAttribute", throwOnError: true)!;
        var obsolete = attributeType
            .GetCustomAttributes(typeof(ObsoleteAttribute), false);
        obsolete.ShouldNotBeEmpty();
    }

    /// <summary>
    /// 测试目的：AttributeUsage 应允许 Class、Method、Property 三个目标。
    /// </summary>
    [Fact]
    public void DisableAuditingAttribute_AttributeUsage_ShouldAllowClassMethodProperty()
    {
        var attributeType = typeof(AuditLogInfo).Assembly.GetType("Bing.Auditing.DisableAuditingAttribute", throwOnError: true)!;
        var usage = (AttributeUsageAttribute)attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        (usage.ValidOn & AttributeTargets.Class).ShouldBe(AttributeTargets.Class);
        (usage.ValidOn & AttributeTargets.Method).ShouldBe(AttributeTargets.Method);
        (usage.ValidOn & AttributeTargets.Property).ShouldBe(AttributeTargets.Property);
    }
}

// =========================================================================
//  SimpleLogAuditingStore Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 SimpleLogAuditingStore 使用 NullLogger 且 SaveAsync 不抛异常。
/// </summary>
public class SimpleLogAuditingStoreTest
{
    /// <summary>
    /// 测试目的：默认构造后 Logger 应为 NullLogger 实例。
    /// </summary>
    [Fact]
    public void Constructor_ShouldUseNullLogger()
    {
        var store = new SimpleLogAuditingStore();
        store.Logger.ShouldNotBeNull();
        store.Logger.ShouldBeSameAs(NullLogger<SimpleLogAuditingStore>.Instance);
    }

    /// <summary>
    /// 测试目的：SaveAsync 对合法的 AuditLogInfo 不应抛出任何异常。
    /// </summary>
    [Fact]
    public async Task SaveAsync_ValidAuditLogInfo_ShouldNotThrow()
    {
        // Arrange
        var store = new SimpleLogAuditingStore();
        var info = new AuditLogInfo
        {
            HttpMethod = "GET",
            Url = "/api/test",
            ExecutionDuration = 5
        };

        // Act & Assert
        await Should.NotThrowAsync(() => store.SaveAsync(info));
    }

    /// <summary>
    /// 测试目的：SaveAsync 返回已完成的 Task（源码 Task.FromResult(0)）。
    /// </summary>
    [Fact]
    public async Task SaveAsync_ShouldReturnCompletedTask()
    {
        var store = new SimpleLogAuditingStore();
        var info = new AuditLogInfo { HttpMethod = "POST", Url = "/api/orders" };

        var task = store.SaveAsync(info);
        task.IsCompleted.ShouldBeTrue();
        await task; // no exception
    }
}
