using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Generated;

namespace Bing.Data.Sql.Aot.PackageModels;

// 模型程序集独立生成和编译，宿主只消费公开快照；这里没有任何查询调用点。
[SqlModel(typeof(CrudRow))]
[SqlModel(typeof(VersionedCrudRow))]
[SqlModel(typeof(IdentityCrudRow))]
[SqlModel(typeof(IdentityOnlyCrudRow))]
[SqlModel(typeof(SelectedCrudRow))]
[SqlResult(typeof(RawRow))]
[SqlResult(typeof(ReturningProjectionDto))]
[SqlParameters(typeof(RawFilter))]
public static partial class PackageModels { }

[Table("package_rows")]
public sealed class CrudRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int Id { get; set; }
    public string Name { get; set; }
}

/// <summary>验证完整生成 CRUD SQL 的复合键、并发条件和 NULL 参数。</summary>
[Table("versioned_rows")]
public sealed class VersionedCrudRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int TenantId { get; set; }
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int Id { get; set; }
    public string Name { get; set; }
    [ConcurrencyCheck] public int Version { get; set; }
}
/// <summary>验证独立模型程序集生成身份键 Insert/Returning SQL。</summary>
[Table("identity_crud_rows")]
public sealed class IdentityCrudRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
    public string Name { get; set; }
}

/// <summary>验证无输入参数的 DEFAULT VALUES 生成键路径。</summary>
[Table("identity_only_crud_rows")]
public sealed class IdentityOnlyCrudRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
}

/// <summary>验证任意多列写入和整行返回共用生成的片段与静态绑定。</summary>
[Table("selected_crud_rows")]
public sealed class SelectedCrudRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
    public string Name { get; set; }
    public int Quantity { get; set; }
    [ConcurrencyCheck] public int Version { get; set; }
}
public sealed class RawRow { public int Id { get; set; } public string Name { get; set; } }
public sealed class RawFilter { public int Minimum { get; set; } }

/// <summary>由独立模型程序集生成静态 Returning DTO 读取器。</summary>
public sealed class ReturningProjectionDto
{
    [Column("row_id")] public int Id { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = "initial-display";
    [Column("optional_note")] public string OptionalNote { get; set; } = "initial-note";
}
