using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;
using Bing.Validation;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var dynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported;
Console.WriteLine("运行模式: " + (dynamicCodeSupported ? "JIT" : "NativeAOT") +
                  $" (RuntimeFeature.IsDynamicCodeSupported={dynamicCodeSupported})");
if (args.Contains("--require-aot", StringComparer.Ordinal))
    Require(!dynamicCodeSupported, "NativeAOT 运行时禁用动态代码");

var tracker = new ChangeGraphTracker();
var beforeOrder = CreateOrder(updated: false);
var afterOrder = CreateOrder(updated: true);
var orderResult = tracker.Compare(beforeOrder, afterOrder);
Console.WriteLine("订单变更路径: " + string.Join(", ", orderResult.Changes.Select(change => change.Path.Value)));
var expectedOrderPaths = new HashSet<string>(StringComparer.Ordinal)
{
    "Customer.Name",
    "PrimaryContact.Name",
    "Lines[11].Amount",
    "Lines[11].Product.Name",
    "Lines[12]",
    "Lines[13]"
};
Require(expectedOrderPaths.SetEquals(orderResult.Changes.Select(change => change.Path.Value)) &&
        orderResult.Changes.Count == expectedOrderPaths.Count,
    "订单的客户、嵌套商品和明细增删路径");

var changesByPath = orderResult.Changes.ToDictionary(change => change.Path.Value, StringComparer.Ordinal);
Require(changesByPath["Customer.Name"].OldValue.GetScalar<string>() == "客户-旧" &&
        changesByPath["Customer.Name"].NewValue.GetScalar<string>() == "客户-新" &&
        changesByPath["Customer.Name"].Description == "客户名称",
    "客户描述及旧、新值来自 Customer 自身规则");
Require(changesByPath["PrimaryContact.Name"].OldValue.GetScalar<string>() == "客户-旧" &&
        changesByPath["PrimaryContact.Name"].NewValue.GetScalar<string>() == "客户-新",
    "同一客户实例通过两条关系路径分别报告");
Require(changesByPath["Lines[11].Amount"].OldValue.GetScalar<decimal>() == 10m &&
        changesByPath["Lines[11].Amount"].NewValue.GetScalar<decimal>() == 12.5m &&
        changesByPath["Lines[11].Amount"].Description == "行金额",
    "集合元素按领域身份键匹配并读取明细自身描述");
Require(changesByPath["Lines[11].Product.Name"].OldValue.GetScalar<string>() == "商品-旧" &&
        changesByPath["Lines[11].Product.Name"].NewValue.GetScalar<string>() == "商品-新" &&
        changesByPath["Lines[11].Product.Name"].Description == "商品名称",
    "明细的子对象使用 Product 自身规则");
Require(changesByPath["Lines[12]"].Kind == ChangeKind.Removed &&
        changesByPath["Lines[12]"].OldValue.Members["Id"].GetScalar<int>() == 12 &&
        changesByPath["Lines[13]"].Kind == ChangeKind.Added &&
        changesByPath["Lines[13]"].NewValue.Members["Id"].GetScalar<int>() == 13,
    "按领域身份键报告明细删除和新增");
Require(!orderResult.Changes.Any(change => change.Path.Value.Contains("InternalCode", StringComparison.Ordinal)),
    "Customer 与 Product 各自忽略内部代码");

var domainObjectChanges = beforeOrder.GetChanges(afterOrder).ToDictionary(change => change.PropertyName, StringComparer.Ordinal);
Require(domainObjectChanges["Customer.Name"].OldValue == "\"客户-旧\"" &&
        domainObjectChanges["Customer.Name"].OldValue == "\"客户-旧\"" &&
        domainObjectChanges["Customer.Name"].NewValue == "\"客户-新\"" &&
        domainObjectChanges["Lines[11].Product.Name"].OldValue == "\"商品-旧\"" &&
        domainObjectChanges["Lines[11].Product.Name"].NewValue == "\"商品-新\"",
    "DomainObjectBase<Order>.GetChanges 通过逐成员 hook 汇总嵌套路径");

var beforeDepartment = CreateDepartment("部门-旧", "子部门-旧");
var afterDepartment = CreateDepartment("部门-新", "子部门-新");
var departmentResult = tracker.Compare(beforeDepartment, afterDepartment);
var expectedDepartmentPaths = new HashSet<string>(StringComparer.Ordinal)
{
    "Name",
    "Children[2].Name"
};
Require(expectedDepartmentPaths.SetEquals(departmentResult.Changes.Select(change => change.Path.Value)) &&
        departmentResult.Changes.Count == expectedDepartmentPaths.Count,
    "Department 的父子双向关系与 Parent 自引用有限终止");

var mutableOrder = CreateOrder(updated: false);
var frozen = tracker.Capture(mutableOrder);
mutableOrder.Customer.Name = "采集后修改";
mutableOrder.Lines[0].Amount = 99m;
var frozenResult = tracker.CompareSnapshot(frozen, mutableOrder);
var frozenChanges = frozenResult.Changes.ToDictionary(change => change.Path.Value, StringComparer.Ordinal);
Require(frozenChanges["Customer.Name"].OldValue.GetScalar<string>() == "客户-旧" &&
        frozenChanges["Customer.Name"].NewValue.GetScalar<string>() == "采集后修改" &&
        frozenChanges["Lines[11].Amount"].OldValue.GetScalar<decimal>() == 10m &&
        frozenChanges["Lines[11].Amount"].NewValue.GetScalar<decimal>() == 99m,
    "Capture 快照冻结采集时的嵌套对象与集合值");

var legacyChanges = new LegacySample { Name = "old" }.GetChanges(new LegacySample { Name = "new" });
var legacyChange = legacyChanges.Single();
Require(legacyChange.PropertyName == "Name" && legacyChange.Description == "旧版名称" &&
        legacyChange.OldValue == "\"old\"" && legacyChange.NewValue == "\"new\"",
    "Legacy DomainObjectBase 入口输出描述和四参数 AddChange 结果");

DomainNestedScenarios.Run();
ReviewScenarios.Run();
HookOverrideScenarios.Run();
PrintGeneratedSources();
Console.WriteLine("全部 Domain ChangeGraph Smoke 检查通过。");

static void Require(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException("Smoke 检查失败: " + description);
    Console.WriteLine("PASS | " + description);
}

static Order CreateOrder(bool updated)
{
    var customer = new Customer
    {
        Id = 7,
        Name = updated ? "客户-新" : "客户-旧",
        InternalCode = updated ? "secret-new" : "secret-old"
    };
    var product = new Product
    {
        Id = 70,
        Name = updated ? "商品-新" : "商品-旧",
        InternalCode = updated ? "product-secret-new" : "product-secret-old"
    };
    var lines = new List<Line>
    {
        new() { Id = 11, Amount = updated ? 12.5m : 10m, Product = product }
    };
    if (updated)
    {
        lines.Add(new Line
        {
            Id = 13,
            Amount = 3m,
            Product = new Product { Id = 71, Name = "新增商品", InternalCode = "ignored" }
        });
    }
    else
    {
        lines.Add(new Line
        {
            Id = 12,
            Amount = 5m,
            Product = new Product { Id = 72, Name = "待删除商品", InternalCode = "ignored" }
        });
    }

    return new Order
    {
        Id = 9001,
        Customer = customer,
        PrimaryContact = customer,
        Lines = lines
    };
}

static Department CreateDepartment(string rootName, string childName)
{
    var root = new Department { Id = 1, Name = rootName };
    root.Parent = root;
    var child = new Department { Id = 2, Name = childName, Parent = root };
    root.Children.Add(child);
    return root;
}

static void PrintGeneratedSources()
{
    // 由 csproj 注入实际构建配置与目标框架，避免固定写法与实际生成目录不一致。
    var metadata = typeof(ProgramMarker).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .Cast<System.Reflection.AssemblyMetadataAttribute>()
        .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    var configuration = metadata.TryGetValue("SmokeConfiguration", out var config) ? config : "Debug";
    var targetFramework = metadata.TryGetValue("SmokeTargetFramework", out var tfm) ? tfm : "net10.0";
    var directory = metadata.TryGetValue("SmokeGeneratedRoot", out var root)
        ? root
        : "framework/tests/Bing.Ddd.Domain.Aot.Smoke/Generated/" + configuration + "/" + targetFramework;
    const string generator = "Bing.Ddd.Domain.Extensions.Analyzers/Bing.Analyzers.ChangeTrackableSourceGenerator";
    Console.WriteLine("生成文件目录: " + directory);
    foreach (var name in new[]
             {
                 "Order.AddChanges.g.cs",
                 "Customer.AddChanges.g.cs",
                 "Line.AddChanges.g.cs",
                 "Product.AddChanges.g.cs",
                 "Department.AddChanges.g.cs",
                 "LegacySample.AddChanges.g.cs"
             })
        Console.WriteLine("  " + directory + "/" + generator + "/" + name);
}

/// <summary>用于读取程序集元数据的标记类型。</summary>
public sealed class ProgramMarker { }


public partial class Order : DomainObjectBase<Order>
{
    public int Id { get; set; }
    public Customer Customer { get; set; } = null!;
    public Customer PrimaryContact { get; set; } = null!;
    public List<Line> Lines { get; set; } = new();

    public override IValidationResult Validate() => null!;
}

public partial class Customer : DomainObjectBase<Customer>, IEntity
{
    public int Id { get; set; }
    [Description("客户名称")]
    public string Name { get; set; } = string.Empty;
    [ChangeIgnore]
    public string InternalCode { get; set; } = string.Empty;
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

public partial class Line : DomainObjectBase<Line>, IEntity
{
    public int Id { get; set; }
    [Display(Name = "行金额")]
    public decimal Amount { get; set; }
    public Product Product { get; set; } = null!;
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

public partial class Product : DomainObjectBase<Product>, IEntity
{
    public int Id { get; set; }
    [Description("商品名称")]
    public string Name { get; set; } = string.Empty;
    [ChangeIgnore]
    public string InternalCode { get; set; } = string.Empty;
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

public partial class Department : DomainObjectBase<Department>, IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Department? Parent { get; set; }
    public List<Department> Children { get; set; } = new();
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

public partial class LegacySample : DomainObjectBase<LegacySample>
{
    [Description("旧版名称")]
    public string Name { get; set; } = string.Empty;

    public override IValidationResult Validate() => null!;
}
