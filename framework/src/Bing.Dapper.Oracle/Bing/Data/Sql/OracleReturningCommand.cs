using System.Collections.Generic;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using Oracle.ManagedDataAccess.Client;

namespace Bing.Data.Sql;

/// <summary>Oracle 写入回传命令。SQL 和参数在创建后冻结。</summary>
public sealed class OracleReturningCommand
{
    internal OracleReturningCommand(SqlWriteCommand command, int maxRows,
        (string Name, OracleDbType Type, int? Size)[] bulkOutputs)
    {
        Command = command;
        MaxRows = maxRows;
        BulkOutputs = bulkOutputs;
    }

    internal SqlWriteCommand Command { get; }

    internal int MaxRows { get; }

    internal IReadOnlyList<(string Name, OracleDbType Type, int? Size)> BulkOutputs { get; }

    /// <summary>获取要执行的 PL/SQL 文本。</summary>
    public string Sql => Command.Sql;

    /// <summary>获取独立的输入和输出参数快照。</summary>
    public IReadOnlyCollection<SqlParam> Parameters => Command.CreateParameters();
}
