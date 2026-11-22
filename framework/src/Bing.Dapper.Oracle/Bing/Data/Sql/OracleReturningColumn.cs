using System;
using System.Data;

namespace Bing.Data.Sql;

/// <summary>Oracle 写入要回传的物理列和输出参数类型。</summary>
public sealed class OracleReturningColumn
{
    /// <summary>定义要回传的列。</summary>
    /// <param name="column">物理列名。</param>
    /// <param name="dbType">输出参数所用的 ADO.NET 类型。</param>
    /// <param name="size">字符或二进制输出长度。</param>
    /// <param name="name">读取输出时使用的参数名，默认为物理列名。</param>
    public OracleReturningColumn(string column, DbType dbType, int? size = null, string name = null)
    {
        if (string.IsNullOrWhiteSpace(column) || column.Trim() == "*")
            throw new ArgumentException("ReturningInto 需要物理列名。", nameof(column));
        if ((dbType is DbType.String or DbType.StringFixedLength or DbType.AnsiString or DbType.AnsiStringFixedLength or DbType.Binary) &&
            !(size > 0))
            throw new ArgumentException("字符和二进制输出必须指定正数长度。", nameof(size));

        Column = column;
        DbType = dbType;
        Size = size;
        Name = name ?? column;
    }

    /// <summary>获取物理列名。</summary>
    public string Column { get; }

    /// <summary>获取输出参数类型。</summary>
    public DbType DbType { get; }

    /// <summary>获取输出参数长度。</summary>
    public int? Size { get; }

    /// <summary>获取输出参数名。</summary>
    public string Name { get; }
}
