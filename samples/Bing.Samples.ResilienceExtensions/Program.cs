using System.Text;
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
if (args.Contains("--management")) { await ManagementDemo.Run(); return; }
await CoreDemo.Run();
await HttpDemo.Run();
await DurableDemo.Run();
if (args.Contains("--redis")) await DistributedDemo.Run();
Console.WriteLine("PASS：本地容错扩展示例完成。");
