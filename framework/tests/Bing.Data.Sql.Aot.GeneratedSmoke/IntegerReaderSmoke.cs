using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Extensions.DependencyInjection;

/// <summary>在默认转换管线中验证生成整数 Reader，以及基类登记的写入访问器。</summary>
internal static class IntegerReaderSmoke
{
    internal static async Task Run(string path)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
        services.AddSqlGeneratedModels(GeneratedModels.CreateModels());
        services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=" + path);
        services.AddSqliteProvider();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        const string sql = "SELECT -2147483648 AS IntValue, -32768 AS ShortValue, 255 AS ByteValue, " +
            "-128 AS SByteValue, 65535 AS UShortValue, 4294967295 AS UIntValue, " +
            "9223372036854775807 AS ULongValue, 2147483647 AS NullableInt, " +
            "32767 AS NullableShort, 255 AS NullableByte, 127 AS NullableSByte, " +
            "65535 AS NullableUShort, 4294967295 AS NullableUInt, 9223372036854775807 AS NullableULong";
        var row = query.Sql(sql).Single<IntegerResult>();
        Check(row.IntValue == int.MinValue && row.ShortValue == short.MinValue && row.ByteValue == byte.MaxValue &&
            row.SByteValue == sbyte.MinValue && row.UShortValue == ushort.MaxValue && row.UIntValue == uint.MaxValue &&
            row.ULongValue == long.MaxValue, "generated integer reader boundaries");
        Check(row.NullableInt == int.MaxValue && row.NullableShort == short.MaxValue && row.NullableByte == byte.MaxValue &&
            row.NullableSByte == sbyte.MaxValue && row.NullableUShort == ushort.MaxValue && row.NullableUInt == uint.MaxValue &&
            row.NullableULong == long.MaxValue, "generated nullable integer reader boundaries");
        Check((await query.Sql(sql).SingleAsync<IntegerResult>()).UShortValue == ushort.MaxValue,
            "generated integer reader async");
        var defaults = query.Sql("SELECT NULL AS ByteValue, NULL AS NullableByte").Single<IntegerResult>();
        Check(defaults.ByteValue == 7 && defaults.NullableByte == 7, "generated integer reader NULL preserves defaults");
        Check(query.Sql("SELECT '255' AS ByteValue, '-128' AS NullableSByte").Single<IntegerResult>() is
            { ByteValue: 255, NullableSByte: -128 }, "generated integer reader string fallback");
        var mixed = query.Sql("SELECT 127 AS ByteValue UNION ALL SELECT '128' UNION ALL SELECT NULL UNION ALL SELECT 1.5")
            .ToList<IntegerResult>();
        Check(mixed.Select(value => value.ByteValue).SequenceEqual(new byte[] { 127, 128, 7, 2 }),
            "generated integer reader checks mixed storage per row");
        foreach (var (name, value) in new[]
        {
            ("ShortValue", "32768"), ("ByteValue", "256"), ("SByteValue", "128"),
            ("UShortValue", "65536"), ("UIntValue", "4294967296"), ("ULongValue", "-1"),
            ("NullableByte", "-1")
        })
        {
            var overflow = false;
            try { query.Sql("SELECT " + value + " AS " + name).Single<IntegerResult>(); }
            catch (OverflowException) { overflow = true; }
            Check(overflow, "generated integer reader overflow " + name);
        }

        using var writer = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
        writer.ExecuteSql("CREATE TABLE DerivedMutationRows(Id INTEGER PRIMARY KEY, Name TEXT)");
        var derived = new MutationDerivedRow { Id = 1, Name = "hidden" };
        ((MutationBaseRow)derived).Name = "base-insert";
        Check(writer.Insert<MutationBaseRow>(derived) == 1 &&
            query.Sql("SELECT Name FROM DerivedMutationRows WHERE Id=1").Scalar<string>() == "base-insert",
            "generated base insert accepts derived instance and base member");
        ((MutationBaseRow)derived).Name = "base-update";
        Check(await writer.UpdateAsync<MutationBaseRow>(derived) == 1 &&
            query.Sql("SELECT Name FROM DerivedMutationRows WHERE Id=1").Scalar<string>() == "base-update",
            "generated base update accepts derived instance and base member");
        Check(writer.Delete<MutationBaseRow>(derived) == 1 &&
            query.Sql("SELECT COUNT(*) FROM DerivedMutationRows").Scalar<int>() == 0,
            "generated base delete accepts derived instance");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }
}

public sealed class IntegerResult
{
    public int IntValue { get; set; }
    public short ShortValue { get; set; }
    public byte ByteValue { get; set; } = 7;
    public sbyte SByteValue { get; set; }
    public ushort UShortValue { get; set; }
    public uint UIntValue { get; set; }
    public ulong ULongValue { get; set; }
    public int? NullableInt { get; set; }
    public short? NullableShort { get; set; }
    public byte? NullableByte { get; set; } = 7;
    public sbyte? NullableSByte { get; set; }
    public ushort? NullableUShort { get; set; }
    public uint? NullableUInt { get; set; }
    public ulong? NullableULong { get; set; }
}

[Table("DerivedMutationRows")]
public class MutationBaseRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int Id { get; set; }
    public string Name { get; set; }
}

public sealed class MutationDerivedRow : MutationBaseRow
{
    public new string Name { get; set; }
}
