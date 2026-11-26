using System.Text;
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("fixture-stdout-中文");
Console.Error.WriteLine("fixture-stderr-中文");
if (args is ["--window"] or ["--mixed-stage-probe", "--window"])
{
    // 复用实际调度器；夹具不连接数据库，也不冒充 SQL 方法证据。
    await Bing.Data.Sql.Benchmarks.SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(200), 1, 90, 30,
        (_, _) => { Thread.SpinWait(10000); return Task.CompletedTask; });
    await Bing.Data.Sql.Benchmarks.SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(300), 1, 90, 30,
        (_, _) => { Thread.SpinWait(10000); return Task.CompletedTask; }, traceMeasurement: true);
    Thread.SpinWait(1000000);
    return 0;
}
if (args is ["--mixed-stage-probe", "--no-window"])
{
    Thread.SpinWait(1000000);
    return 0;
}
if (Environment.GetEnvironmentVariable("BING_SQL_CPU_FIXTURE") == "timeout") Thread.Sleep(300000);
return 23;
