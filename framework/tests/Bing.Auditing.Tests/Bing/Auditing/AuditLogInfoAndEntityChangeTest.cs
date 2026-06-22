using Bing.Auditing;
using Shouldly;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// <see cref="AuditLogInfo"/>、<see cref="AuditLogActionInfo"/>、
/// <see cref="EntityChangeInfo"/>、<see cref="EntityPropertyChangeInfo"/>、
/// <see cref="AuditedPropertyConst"/> 单元测试。
/// </summary>
public class AuditLogInfoAndEntityChangeTest
{
    // ═══════════════════════════════════════════════════════════
    // AuditLogInfo — 默认构造与集合初始化
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后所有 List 集合属性不为 null，保证调用方直接 Add 不会 NullReferenceException。
    /// </summary>
    [Fact]
    public void AuditLogInfo_DefaultCtor_AllListsShouldNotBeNull()
    {
        // Arrange & Act
        var info = new AuditLogInfo();

        // Assert
        info.Actions.ShouldNotBeNull();
        info.Exceptions.ShouldNotBeNull();
        info.EntityChanges.ShouldNotBeNull();
        info.Comments.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：默认构造后所有 List 集合应为空（不含任何元素）。
    /// </summary>
    [Fact]
    public void AuditLogInfo_DefaultCtor_AllListsShouldBeEmpty()
    {
        // Arrange & Act
        var info = new AuditLogInfo();

        // Assert
        info.Actions.ShouldBeEmpty();
        info.Exceptions.ShouldBeEmpty();
        info.EntityChanges.ShouldBeEmpty();
        info.Comments.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：ToString() 在无 Actions/Exceptions/EntityChanges 时，应包含 HTTP 基础信息行，不抛异常。
    /// </summary>
    [Fact]
    public void AuditLogInfo_ToString_WithBasicInfoOnly_ShouldContainHttpInfo()
    {
        // Arrange
        var info = new AuditLogInfo
        {
            HttpMethod = "GET",
            HttpStatusCode = 200,
            Url = "/api/orders",
            UserName = "alice",
            UserId = "u-001",
            ClientIpAddress = "192.168.1.1",
            ExecutionDuration = 120
        };

        // Act
        var result = info.ToString();

        // Assert
        result.ShouldContain("GET");
        result.ShouldContain("200");
        result.ShouldContain("/api/orders");
        result.ShouldContain("alice");
        result.ShouldContain("u-001");
        result.ShouldContain("192.168.1.1");
        result.ShouldContain("120");
    }

    /// <summary>
    /// 测试目的：ToString() 在含有 Actions 时，应输出 ServiceName、MethodName 和执行耗时。
    /// </summary>
    [Fact]
    public void AuditLogInfo_ToString_WithActions_ShouldContainActionInfo()
    {
        // Arrange
        var info = new AuditLogInfo();
        info.Actions.Add(new AuditLogActionInfo
        {
            ServiceName = "OrderAppService",
            MethodName = "CreateOrder",
            ExecutionDuration = 55,
            Parameters = "{\"orderId\":\"o-001\"}"
        });

        // Act
        var result = info.ToString();

        // Assert
        result.ShouldContain("OrderAppService");
        result.ShouldContain("CreateOrder");
        result.ShouldContain("55");
        result.ShouldContain("{\"orderId\":\"o-001\"}");
    }

    /// <summary>
    /// 测试目的：ToString() 在含有 Exceptions 时，应包含异常消息。
    /// </summary>
    [Fact]
    public void AuditLogInfo_ToString_WithExceptions_ShouldContainExceptionMessage()
    {
        // Arrange
        var info = new AuditLogInfo();
        info.Exceptions.Add(new InvalidOperationException("order not found"));

        // Act
        var result = info.ToString();

        // Assert
        result.ShouldContain("order not found");
    }

    /// <summary>
    /// 测试目的：ToString() 在含有 EntityChanges 时，应包含实体类型全名和实体 ID。
    /// </summary>
    [Fact]
    public void AuditLogInfo_ToString_WithEntityChanges_ShouldContainEntityInfo()
    {
        // Arrange
        var info = new AuditLogInfo();
        info.EntityChanges.Add(new EntityChangeInfo
        {
            EntityTypeFullName = "Bing.Domain.Orders.Order",
            EntityId = "order-abc",
            PropertyChanges = new List<EntityPropertyChangeInfo>
            {
                new EntityPropertyChangeInfo
                {
                    PropertyName = "Status",
                    OriginalValue = "Created",
                    NewValue = "Completed"
                }
            }
        });

        // Act
        var result = info.ToString();

        // Assert
        result.ShouldContain("Bing.Domain.Orders.Order");
        result.ShouldContain("order-abc");
        result.ShouldContain("Status");
        result.ShouldContain("Created");
        result.ShouldContain("Completed");
    }

    /// <summary>
    /// 测试目的：HttpStatusCode 为 null 时 ToString() 应使用占位符，不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void AuditLogInfo_ToString_NullHttpStatusCode_ShouldUsePlaceholder()
    {
        // Arrange
        var info = new AuditLogInfo { HttpStatusCode = null };

        // Act & Assert
        Should.NotThrow(() => info.ToString());
        info.ToString().ShouldContain("---");
    }

    // ═══════════════════════════════════════════════════════════
    // AuditLogActionInfo — DTO 属性读写
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AuditLogActionInfo 所有属性可读写，默认值均为 null/0。
    /// </summary>
    [Fact]
    public void AuditLogActionInfo_Properties_ShouldBeReadWritable()
    {
        // Arrange
        var action = new AuditLogActionInfo();

        // Act
        action.ServiceName = "ProductService";
        action.MethodName = "GetById";
        action.Parameters = "{\"id\":1}";
        action.ExecutionTime = new DateTime(2026, 1, 1, 12, 0, 0);
        action.ExecutionDuration = 30;

        // Assert
        action.ServiceName.ShouldBe("ProductService");
        action.MethodName.ShouldBe("GetById");
        action.Parameters.ShouldBe("{\"id\":1}");
        action.ExecutionDuration.ShouldBe(30);
    }

    // ═══════════════════════════════════════════════════════════
    // EntityChangeInfo.Merge — 合并逻辑
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Merge 时若传入的属性变更在目标中不存在，应将其添加进 PropertyChanges。
    /// </summary>
    [Fact]
    public void EntityChangeInfo_Merge_NewProperty_ShouldBeAdded()
    {
        // Arrange
        var target = new EntityChangeInfo
        {
            PropertyChanges = new List<EntityPropertyChangeInfo>()
        };
        var incoming = new EntityChangeInfo
        {
            PropertyChanges = new List<EntityPropertyChangeInfo>
            {
                new EntityPropertyChangeInfo { PropertyName = "Name", OriginalValue = null, NewValue = "Alice" }
            }
        };

        // Act
        target.Merge(incoming);

        // Assert
        target.PropertyChanges.Count.ShouldBe(1);
        target.PropertyChanges[0].PropertyName.ShouldBe("Name");
        target.PropertyChanges[0].NewValue.ShouldBe("Alice");
    }

    /// <summary>
    /// 测试目的：Merge 时若传入的属性变更在目标中已存在（同名），应更新 NewValue，而非重复添加。
    /// </summary>
    [Fact]
    public void EntityChangeInfo_Merge_ExistingProperty_ShouldUpdateNewValue()
    {
        // Arrange
        var existing = new EntityPropertyChangeInfo
        {
            PropertyName = "Status",
            OriginalValue = "Draft",
            NewValue = "Pending"
        };
        var target = new EntityChangeInfo
        {
            PropertyChanges = new List<EntityPropertyChangeInfo> { existing }
        };
        var incoming = new EntityChangeInfo
        {
            PropertyChanges = new List<EntityPropertyChangeInfo>
            {
                new EntityPropertyChangeInfo { PropertyName = "Status", OriginalValue = "Pending", NewValue = "Completed" }
            }
        };

        // Act
        target.Merge(incoming);

        // Assert
        target.PropertyChanges.Count.ShouldBe(1);
        target.PropertyChanges[0].NewValue.ShouldBe("Completed");
    }

    /// <summary>
    /// 测试目的：Merge 时同时含有新属性和已有属性：新属性被追加，已有属性 NewValue 被更新。
    /// </summary>
    [Fact]
    public void EntityChangeInfo_Merge_MixedProperties_ShouldAddAndUpdate()
    {
        // Arrange
        var target = new EntityChangeInfo
        {
            PropertyChanges = new List<EntityPropertyChangeInfo>
            {
                new EntityPropertyChangeInfo { PropertyName = "Name", OriginalValue = "Alice", NewValue = "Bob" }
            }
        };
        var incoming = new EntityChangeInfo
        {
            PropertyChanges = new List<EntityPropertyChangeInfo>
            {
                new EntityPropertyChangeInfo { PropertyName = "Name", OriginalValue = "Bob", NewValue = "Charlie" },
                new EntityPropertyChangeInfo { PropertyName = "Age", OriginalValue = null, NewValue = "30" }
            }
        };

        // Act
        target.Merge(incoming);

        // Assert
        target.PropertyChanges.Count.ShouldBe(2);
        target.PropertyChanges.First(p => p.PropertyName == "Name").NewValue.ShouldBe("Charlie");
        target.PropertyChanges.First(p => p.PropertyName == "Age").NewValue.ShouldBe("30");
    }

    // ═══════════════════════════════════════════════════════════
    // EntityPropertyChangeInfo — DTO 属性读写
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：EntityPropertyChangeInfo 所有属性可读写，默认值均为 null。
    /// </summary>
    [Fact]
    public void EntityPropertyChangeInfo_Properties_ShouldBeReadWritable()
    {
        // Arrange
        var prop = new EntityPropertyChangeInfo();

        // Act
        prop.PropertyName = "Email";
        prop.PropertyTypeFullName = "System.String";
        prop.OriginalValue = "old@example.com";
        prop.NewValue = "new@example.com";

        // Assert
        prop.PropertyName.ShouldBe("Email");
        prop.PropertyTypeFullName.ShouldBe("System.String");
        prop.OriginalValue.ShouldBe("old@example.com");
        prop.NewValue.ShouldBe("new@example.com");
    }

    // ═══════════════════════════════════════════════════════════
    // AuditedPropertyConst — 默认常量值
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：验证 AuditedPropertyConst 各默认属性名称值正确，以免意外修改破坏数据库字段映射。
    /// </summary>
    [Fact]
    public void AuditedPropertyConst_DefaultValues_ShouldMatchExpected()
    {
        // Assert
        AuditedPropertyConst.Creator.ShouldBe("Creator");
        AuditedPropertyConst.CreatorId.ShouldBe("CreatorId");
        AuditedPropertyConst.CreationTime.ShouldBe("CreationTime");
        AuditedPropertyConst.Modifier.ShouldBe("LastModifier");
        AuditedPropertyConst.ModifierId.ShouldBe("LastModifierId");
        AuditedPropertyConst.ModificationTime.ShouldBe("LastModificationTime");
        AuditedPropertyConst.Version.ShouldBe("Version");
    }
}

/// <summary>
/// <see cref="EntityChangeType"/>（来自 Bing.Auditing.Contracts）单元测试
/// </summary>
public class EntityChangeTypeTest
{
    // ═══════════════════════════════════════════════════════════
    // 枚举值验证
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：枚举各成员的整数值应符合规范（0=Created, 1=Updated, 2=Deleted），
    /// 防止序列化/数据库映射时因数值变动导致历史数据错误。
    /// </summary>
    [Fact]
    public void EntityChangeType_Values_ShouldMatchExpected()
    {
        // Assert
        ((int)EntityChangeType.Created).ShouldBe(0);
        ((int)EntityChangeType.Updated).ShouldBe(1);
        ((int)EntityChangeType.Deleted).ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：枚举成员个数应为 3，防止因新增/删除成员而导致使用方意外受影响（变更感知）。
    /// </summary>
    [Fact]
    public void EntityChangeType_Count_ShouldBeThree()
    {
        // Assert
        Enum.GetValues(typeof(EntityChangeType)).Length.ShouldBe(3);
    }

    /// <summary>
    /// 测试目的：EntityChangeType.Created 应为默认值（0），
    /// 确保未显式赋值时行为可预测。
    /// </summary>
    [Fact]
    public void EntityChangeType_Default_ShouldBeCreated()
    {
        // Assert
        default(EntityChangeType).ShouldBe(EntityChangeType.Created);
    }
}
