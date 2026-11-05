using System.ComponentModel;
using System.Text;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;
using Microsoft.Data.Sqlite;

Console.OutputEncoding = Encoding.UTF8;
using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
Execute(connection, null, "CREATE TABLE Orders (Id INTEGER PRIMARY KEY, Status TEXT NOT NULL, City TEXT NOT NULL); CREATE TABLE Lines (Id INTEGER PRIMARY KEY, OrderId INTEGER NOT NULL, Quantity INTEGER NOT NULL); CREATE TABLE Changes (Path TEXT NOT NULL, Description TEXT NOT NULL, OldValue TEXT, NewValue TEXT);");
Execute(connection, null, "INSERT INTO Orders VALUES (1, '待处理', '上海'); INSERT INTO Lines VALUES (10, 1, 2), (11, 1, 1);");

// 两次加载得到独立快照，避免原地修改导致旧值丢失。
var before = LoadOrder(connection);
var after = LoadOrder(connection);
after.Status = "已确认";
after.Address.City = "杭州";
after.Lines.Single(x => x.Id == 10).Quantity = 5;
after.Lines.RemoveAll(x => x.Id == 11);
after.Lines.Add(new OrderLine { Id = 12, Quantity = 3 });
var changes = before.GetChanges(after);
SaveOrder(connection, after, changes);

var saved = LoadOrder(connection);
var records = ReadChanges(connection);
foreach (var record in records)
    Console.WriteLine($"{record.Path} | {record.Description} | {record.Old} -> {record.New}");

if (args.Contains("--verify"))
{
    var expected = new[] {
        ("Address.City", "城市", "\"上海\"", "\"杭州\""),
        ("Lines[10].Quantity", "数量", "2", "5"),
        ("Lines[11]", "订单明细", "<object>", "<missing>"),
        ("Lines[12]", "订单明细", "<missing>", "<object>"),
        ("Status", "订单状态", "\"待处理\"", "\"已确认\"")
    };
    if (!records.SequenceEqual(expected) || saved.Status != after.Status || saved.Address.City != after.Address.City ||
        !saved.Lines.Select(x => (x.Id, x.Quantity)).SequenceEqual(after.Lines.OrderBy(x => x.Id).Select(x => (x.Id, x.Quantity))))
        throw new InvalidOperationException("订单或持久化的变更记录与预期不一致。");
    Console.WriteLine("订单更新及变更记录验证通过。");

    // 第二条变更写入时由数据库触发失败，确认订单和第一条变更一起回滚。
    var rollbackBefore = LoadOrder(connection);
    var rollbackAfter = LoadOrder(connection);
    rollbackAfter.Status = "已取消";
    rollbackAfter.Address.City = "苏州";
    var rollbackChanges = rollbackBefore.GetChanges(rollbackAfter);
    var rejected = false;
    Execute(connection, null, "CREATE TRIGGER RejectSecondChange BEFORE INSERT ON Changes WHEN (SELECT COUNT(*) FROM Changes) >= 6 BEGIN SELECT RAISE(ABORT, 'expected rollback'); END");
    try
    {
        SaveOrder(connection, rollbackAfter, rollbackChanges);
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.Message.Contains("expected rollback", StringComparison.Ordinal))
    {
        rejected = true;
    }
    finally
    {
        Execute(connection, null, "DROP TRIGGER RejectSecondChange");
    }

    var afterRollback = LoadOrder(connection);
    if (!rejected || afterRollback.Status != saved.Status || afterRollback.Address.City != saved.Address.City ||
        !afterRollback.Lines.Select(x => (x.Id, x.Quantity)).SequenceEqual(saved.Lines.Select(x => (x.Id, x.Quantity))) ||
        !ReadChanges(connection).SequenceEqual(records))
        throw new InvalidOperationException("订单或变更记录未完整回滚。");
    Console.WriteLine("变更记录写入失败时的事务回滚验证通过。");
}

static void SaveOrder(SqliteConnection connection, Order order, ChangedValueDescriptorCollection changes)
{
    using var transaction = connection.BeginTransaction();
    Execute(connection, transaction, "UPDATE Orders SET Status = $status, City = $city WHERE Id = 1", ("$status", order.Status), ("$city", order.Address.City));
    Execute(connection, transaction, "DELETE FROM Lines WHERE OrderId = 1");
    foreach (var line in order.Lines)
        Execute(connection, transaction, "INSERT INTO Lines VALUES ($id, 1, $quantity)", ("$id", line.Id), ("$quantity", line.Quantity));
    foreach (var change in changes)
        Execute(connection, transaction, "INSERT INTO Changes VALUES ($path, $description, $old, $new)",
            ("$path", change.PropertyName), ("$description", change.Description), ("$old", change.OldValue), ("$new", change.NewValue));
    transaction.Commit();
}

static List<(string Path, string Description, string Old, string New)> ReadChanges(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT Path, Description, OldValue, NewValue FROM Changes ORDER BY Path";
    using var reader = command.ExecuteReader();
    var records = new List<(string Path, string Description, string Old, string New)>();
    while (reader.Read())
        records.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
    return records;
}

static Order LoadOrder(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT Id, Status, City FROM Orders WHERE Id = 1";
    using var reader = command.ExecuteReader();
    if (!reader.Read()) throw new InvalidOperationException("订单不存在。");
    var order = new Order { Id = reader.GetInt32(0), Status = reader.GetString(1), Address = new Address { City = reader.GetString(2) } };
    reader.Close();
    command.CommandText = "SELECT Id, Quantity FROM Lines WHERE OrderId = 1 ORDER BY Id";
    using var lines = command.ExecuteReader();
    while (lines.Read()) order.Lines.Add(new OrderLine { Id = lines.GetInt32(0), Quantity = lines.GetInt32(1) });
    return order;
}

static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
{
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = sql;
    foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
    command.ExecuteNonQuery();
}

/// <summary>订单聚合，每个领域对象生成自己的比较规则。</summary>
public partial class Order : DomainObjectBase<Order>
{
    public int Id { get; set; }
    [Description("订单状态")] public string Status { get; set; } = "";
    [Description("收货地址")] public Address Address { get; set; } = new();
    [Description("订单明细")] public List<OrderLine> Lines { get; set; } = new();
}

/// <summary>收货地址。</summary>
public partial class Address : DomainObjectBase<Address>
{
    [Description("城市")] public string City { get; set; } = "";
}

/// <summary>订单明细按自身实体键匹配。</summary>
public partial class OrderLine : DomainObjectBase<OrderLine>, IEntity
{
    public int Id { get; set; }
    [Description("数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}
