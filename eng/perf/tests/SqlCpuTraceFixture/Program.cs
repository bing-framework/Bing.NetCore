using System.Text;
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("fixture-stdout-中文");
Console.Error.WriteLine("fixture-stderr-中文");
if (Environment.GetEnvironmentVariable("BING_SQL_CPU_FIXTURE") == "timeout") Thread.Sleep(300000);
return 23;
