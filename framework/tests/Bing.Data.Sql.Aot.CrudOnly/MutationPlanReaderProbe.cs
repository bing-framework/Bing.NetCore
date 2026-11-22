using Bing.Data.Sql.Aot.PackageModels;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Mutations;

/// <summary>验证 Native 严格模式只使用当前写入映射的静态读取计划。</summary>
internal static class MutationPlanReaderProbe
{
    internal static void Verify(DefaultSqlEntityMutationCommandBuilder builder, VersionedCrudRow row)
    {
        var context = builder.CreateUpdateRenderContext(new[] { row }, new SqlUpdateOptions
        {
            IncludeProperties = new[] { nameof(VersionedCrudRow.Name) }
        });
        var column = context.UpdateColumns.Single();
        if (!Equals(context.GetValue(row, column), row.Name))
            throw new Exception("generated batch plan reader");

        // CrudRow 同样有生成读取器，但不能替代 versioned_rows 的映射来源。
        var rejected = false;
        try { context.GetValue(new CrudRow { Name = "unrelated" }, column); }
        catch (NotSupportedException error)
        {
            rejected = error.Message == $"类型 {typeof(VersionedCrudRow).FullName} 的属性 Name 没有匹配当前映射的生成读取器。";
        }
        if (!rejected) throw new Exception("strict mutation mapping boundary");
        Console.WriteLine("MUTATION_PLAN_READER_NATIVE_PASS");
    }
}
