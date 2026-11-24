using System.Linq.Expressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Aot.PackageModels;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Mutations;

/// <summary>真实 Native 执行复用表达式的生成式 Returning，避免每次命令重建表达式树。</summary>
internal static class CachedReturningProbe
{
    private static readonly Expression<Func<CrudRow, object>> Columns = row =>
        new ReturningProjectionDto { Id = row.Id, DisplayName = row.Name };
    private static readonly Expression<Func<CrudRow, object>> ComputedColumns = row =>
        new ReturningProjectionDto { Id = -(row.Id + row.Id), DisplayName = row.Name };

    internal static async Task VerifyAsync(ISqlExecutor writer)
    {
        var direct = writer.CreateUpdateBuilder().Update<CrudRow>().Set("Name", "cached-returned")
            .Where("Id", 1).Returning<CrudRow>(Columns).BuildCommand();
        if (direct.Sql != "Update `package_rows` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning `Id` As `Id`, `Name` As `DisplayName`")
            throw new Exception("cached returning SQL");
        var directRow = writer.ExecuteReturning<ReturningProjectionDto>(direct).Single();
        if (directRow.Id != 1 || directRow.DisplayName != "cached-returned" || directRow.OptionalNote != "initial-note")
            throw new Exception("cached returning result");

        var computed = writer.CreateUpdateBuilder().Update<CrudRow>().Set("Name", "cached-computed")
            .Where("Id", 1).Returning<CrudRow>(ComputedColumns).BuildCommand();
        if (computed.Sql != "Update `package_rows` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning (-(`Id` + `Id`)) As `Id`, `Name` As `DisplayName`")
            throw new Exception("cached computed returning SQL");
        var computedRow = (await writer.ExecuteReturningAsync<ReturningProjectionDto>(computed)).Single();
        if (computedRow.Id != -2 || computedRow.DisplayName != "cached-computed" || computedRow.OptionalNote != "initial-note")
            throw new Exception("cached computed returning result");
        Console.WriteLine("GENERATED_CACHED_RETURNING_NATIVE_PASS");
    }
}
