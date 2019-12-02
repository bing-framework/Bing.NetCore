using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.IdGenerators.Core;
using Bing.Utils.IO;
using Bing.Utils.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests
{
    public class UnitTest1: TestBase
    {
        public UnitTest1(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test1()
        {
            DateTimeOffset offset=DateTimeOffset.Now;
            Output.WriteLine(offset.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [Fact]
        public void Test_GetArrayLowerBound()
        {
            var strs = new string[] {"1", "2", "3", "4", "5"};
            var lowerBound = strs.GetLowerBound(0);
            Output.WriteLine(lowerBound.ToString());
        }

        [Fact]
        public void Test_GetArrayLowerBound_2()
        {            
            string[,] strs = new string[,] {
                {
                    "1","2","3","4","7"
                },
                {
                    "2","3","4","5","6"
                }
            };
            var lowerBound = strs.GetLowerBound(1);
            Output.WriteLine(lowerBound.ToString());
            var upperBound = strs.GetUpperBound(1);
            Output.WriteLine(upperBound.ToString());
        }

        [Fact]
        public void Test_GetArrayUpperBound()
        {
            var strs = new string[] { "1", "2", "3", "4", "5" };
            var upperBound = strs.GetUpperBound(0);
            Output.WriteLine(upperBound.ToString());
        }

        [Fact]
        public void Test_Except()
        {
            var list=new string[]{"1","2","3","4","5"};
            var newList = new string[] {"1", "2"};
            var result=list.Except(newList);
            Output.WriteLine(result.ToJson());
        }

        [Fact]
        public void Test_Except_Int()
        {
            var list = new int[] {1, 3, 5, 7, 9, 11};
            var newList = new int[] {1, 4, 7,8, 9, 11};
            var result = newList.Except(list);
            Output.WriteLine(result.ToJson());
        }

        [Fact]
        public void Test_Except_Guid()
        {
            var oneGuid = Guid.NewGuid();
            var twoGuid = Guid.NewGuid();
            var threeGuid = Guid.NewGuid();
            var fourGuid = Guid.NewGuid();
            var fiveGuid = Guid.NewGuid();
            var sixGuid = Guid.NewGuid();
            var sevenGuid = Guid.NewGuid();
            var list = new Guid[] {oneGuid, twoGuid, threeGuid, fourGuid, fiveGuid, sixGuid, sevenGuid};
            var newList = new Guid[] { fourGuid, fiveGuid, sixGuid, sevenGuid };
            var result = list.Except(newList);
            Output.WriteLine(list.ToJson());
            Output.WriteLine(newList.ToJson());
            Output.WriteLine(result.ToJson());
        }

        [Fact]
        public void Test_GetDomainName()
        {
            string url1 = "https://www.cnblogs.com";
            string url2 = "https://www.cnblogs.com/";
            string url3 = "https://www.cnblogs.com/jeffwongishandsome/archive/2010/10/14/1851217.html";
            string url4 = "http://www.cnblogs.com/jeffwongishandsome/archive/2010/10/14/1851217.html";
            string url5 = "https://www.cnblogs.gz.org/";
            Output.WriteLine(GetDomainName(url1));
            Output.WriteLine(GetDomainName(url2));
            Output.WriteLine(GetDomainName(url3));
            Output.WriteLine(GetDomainName(url4));
            Output.WriteLine(GetDomainName(url5));
        }

        private string GetDomainName(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(nameof(url));
            }
            Regex regex=new Regex(@"(http|https)://(?<domain>[^(:|/]*)", RegexOptions.IgnoreCase);
            return regex.Match(url, 0).Value;
        }

        [Fact]
        public void Test_Id()
        {
            for (int i = 0; i < 1000; i++)
            {
                var id = SequentialGuidGenerator.Current.Create();
                Output.WriteLine($"{id}");
            }
        }

        [Fact]
        public void Test_GetJson()
        {
            string jsonp = @"jsonp({""a"":""1234"",""b"":9999})";
            var json = "{\"a\":\"1234\",\"b\":9999}";
            var result = Regexs.GetValue(jsonp, @"^\w+\((\{[^()]+\})\)$","$1");
            Output.WriteLine(result);
            Assert.Equal(json,result);
        }

        [Fact]
        public void Test_SpaceOnUpper()
        {
            var words = new[] {"StringExtensions", "AA", "AbC", "Cad"};
            foreach (var word in words)
            {
                Output.WriteLine(word.SpaceOnUpper());
            }
        }

        [Fact]
        public void Test_RemoveStrat()
        {
            string path = "/Pages/Home/Index/Pages";
            var index = path.IndexOf(path, StringComparison.OrdinalIgnoreCase);
            var result = path.Remove(index, "/Paegs".Length).Insert(index, "/typings/app");
            Output.WriteLine(result);
        }

        /// <summary>
        /// 生成测试数据
        /// </summary>
        [Fact]
        private void Test_GenerateTestData()
        {
            var dirPath = "D:\\TestData";
            DirectoryHelper.CreateIfNotExists(dirPath);
            FileHelper.Split("D:\\iTestRunner_R1.txt",dirPath);
        }

        /// <summary>
        /// 按前缀分组
        /// </summary>
        [Fact]
        public void Test_GroupByPrefix()
        {
            Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();
            dict.Add("001", new List<string>());
            dict.Add("002", new List<string>());
            dict.Add("003", new List<string>());
            dict.Add("004", new List<string>());
            dict.Add("005", new List<string>());
            dict.Add("006", new List<string>());
            dict.Add("007", new List<string>());
            List<string> list = new List<string>()
            {
                "001.8.desc.json",
                "001.1.desc.json",
                "001.2.desc.json",
                "001.3.desc.json",
                "001.4.desc.json",
                "001.5.desc.json",
                "001.6.desc.json",
                "001.7.desc.json",
                "002.1.desc.json",
                "002.2.desc.json",
                "002.3.desc.json",
                "002.4.desc.json",
                "002.5.desc.json",
                "002.6.desc.json",
                "002.7.desc.json",
                "002.8.desc.json",
                "003.10.desc.json",
                "003.1.desc.json",
                "003.2.desc.json",
                "003.3.desc.json",
                "003.4.desc.json",
                "003.5.desc.json",
                "003.6.desc.json",
                "003.7.desc.json",
                "003.8.desc.json",
                "003.9.desc.json",
                "004.4.desc.json",
                "005.5.desc.json",
                "006.6.desc.json",
                "007.7.desc.json",
            };            

            foreach (var item in list)
            {
                var keys = item.Split('.');
                dict[keys[0]].Add(item);
            }

            foreach (var keyValue in dict)
            {
                var item = keyValue.Value.Max();
                Output.WriteLine($"{keyValue.Key},{item}");
            }
        }

        public enum TestEnum
        {
            A=0,
            B=1,
            C=2
        }

        [Fact]
        public void Test_StringToEnums()
        {
            var enumsStr = "1,2";
            var enums = Conv.ToList<TestEnum>(enumsStr);
            foreach (var item in enums)
            {
                Output.WriteLine(item.ToString());
            }
        }

        /// <summary>
        /// 合并文件
        /// </summary>
        [Fact]
        public void Test_CombineFile()
        {
            var tempMd5 = FileHelper.GetMd5("D:\\iTestRunner_R1.txt");
            //var files = FileUtil.GetAllFiles("D:\\TestData");
            //var outputFilePath = "D:\\iTestRunner_R1_combine_result.txt";
            //FileUtil.Combine(files,outputFilePath);
            //var outputMd5 = FileUtil.GetFileMd5(outputFilePath);

            Output.WriteLine($"temp-md5:{tempMd5}");
            //Output.WriteLine($"new-md5:{outputMd5}");
        }

        /// <summary>
        /// 文件切割
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="outPutPath">输出文件路径</param>
        /// <param name="kbLength">单个子文件最大长度。单位：KB</param>
        /// <param name="delete">标识文件分割完成后是否删除原文件</param>
        /// <param name="change">加密密钥</param>
        private void FileSplit(string filePath,string outPutPath, int kbLength, bool delete, int change)
        {
            if (filePath == null || !File.Exists(filePath))
            {
                return;
            }

            //// 加密初始化
            //short sign = 1;
            //int num = 0, tmp;
            //if (change < 0)
            //{
            //    sign = -1;
            //    change = -change;
            //}

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var fileSize = FileHelper.GetFileSize(filePath);
            var total = Conv.ToInt(fileSize.GetSizeByK() / kbLength);
            using (FileStream readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] data = new byte[1024];// 流读取，缓存空间
                int len = 0, i = 1;// 记录子文件累积读取的KB大小，分割的子文件序号
                int readLen = 0;// 每次实际读取的字节大小
                FileStream writeStream = null;
                // 读取数据
                while (readLen > 0 || (readLen = readStream.Read(data, 0, data.Length)) > 0)
                {                    
                    // 创建分割后的子文件，已有则覆盖
                    if (len == 0 || writeStream == null)
                    {
                        writeStream = new FileStream($"{outPutPath}\\{fileName}.{i++}.{total}.bin", FileMode.Create);
                    }

                    //// 加密逻辑，对data的首字节进行逻辑偏移加密
                    //if (num == 0)
                    //{
                    //    num = change;
                    //}

                    //tmp = data[0] + sign * (num % 3 + 3);
                    //if (tmp > 255)
                    //{
                    //    tmp -= 255;
                    //}
                    //else if(tmp<0)
                    //{
                    //    tmp += 255;
                    //}

                    //data[0] = (byte) tmp;
                    //num /= 3;

                    // 输出，缓存数据写入子文件
                    writeStream.Write(data, 0, readLen);
                    writeStream.Flush();
                    // 预读下一轮缓存数据
                    readLen = readStream.Read(data, 0, data.Length);
                    if (++len >= kbLength || readLen == 0) //子文件达到指定大小，或文件已读完
                    {
                        writeStream.Close();// 关闭当前输出流
                        len = 0;
                    }
                }
            }

            if (delete)
            {
                FileHelper.Delete(filePath);
            }
        }

        /// <summary>
        /// 文件合并
        /// </summary>
        /// <param name="filePaths"></param>
        /// <param name="outFileName"></param>
        /// <param name="delete"></param>
        /// <param name="change"></param>
        private void FileCombine(IList<string> filePaths,string outFileName, bool delete, int change)
        {
            if (filePaths == null || filePaths.Count == 0)
            {
                return;
            }

            short sign = 1;
            //int num = 0, tmp;
            //if (change < 0)
            //{
            //    sign = -1;
            //    change = -change;
            //}

            var keys = Path.GetFileName(filePaths[0]).Split('.');
            var total = keys[2].ToInt();

            using (FileStream writeStream = new FileStream(outFileName, FileMode.Create))
            {
                filePaths.Sort();
                
                foreach (var filePath in filePaths)
                {
                    if (filePath == null || !File.Exists(filePath))
                    {
                        continue;
                    }

                    FileStream readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    byte[] data=new byte[1024];// 流读取，缓存空间
                    int readLen = 0;// 每次实际读取的字节大小

                    // 读取数据
                    while ((readLen=readStream.Read(data,0,data.Length))>0)
                    {
                        //// 解密逻辑，对data的首字节进行逻辑偏移解密
                        //if (num == 0)
                        //{
                        //    num = change;
                        //}

                        //tmp = data[0] + sign * (num % 3 + 3);
                        //if (tmp > 255)
                        //{
                        //    tmp -= 255;
                        //}
                        //else if(tmp<0)
                        //{
                        //    tmp += 255;
                        //}

                        //data[0] = (byte) tmp;
                        //num /= 3;

                        writeStream.Write(data, 0, readLen);
                        writeStream.Flush();
                    }

                    readStream.Close();

                    if (delete)
                    {
                        FileHelper.Delete(filePath);
                    }
                }
            }
        }

        [Fact]
        public void Test_Split()
        {
            var source =
                @"1. Trace: SqlQueryLog >> 跟踪号: 0HLMQ0R8CFGAN:00000001-2    操作时间: 2019-05-17 09:34:47.728    已执行: 1毫秒    
2. IP: ::ffff:172.18.107.80   主机: lebazer01   线程号: 35   
3. 浏览器: Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36   
4. Url: http://172.18.107.84:8101/api/Permission/GetPermissionMenus?t=1558056887443
5. 操作人编号: efa31fb6-d94e-429a-b1fe-6502df67b245   操作人: 超级管理员   
6. 应用程序: 后台管理系统   
7. 类名: Bing.Datas.Dapper.SqlQuery   
8. 标题: SqlQuery查询调试:
9. Sql语句:
原始Sql:
Select `b`.*,`b`.`ResourceId` As `Id` 
From `Systems.Permission` As `a` 
Join `Systems.Resource` As `b` On `a`.`ResourceId`=`b`.`ResourceId` And `b`.`IsDeleted`=@_p_3 
Where `a`.`IsDeny`=@_p_0 And `b`.`ApplicationId`=@_p_1 And `a`.`IsDeleted`=@_p_2
调试Sql:
Select `b`.*,`b`.`ResourceId` As `Id` 
From `Systems.Permission` As `a` 
Join `Systems.Resource` As `b` On `a`.`ResourceId`=`b`.`ResourceId` And `b`.`IsDeleted`=0 
Where `a`.`IsDeny`=1 And `b`.`ApplicationId`='79c3c002-1474-4b3f-bf83-b17aa173a2bb' And `a`.`IsDeleted`=0
10. Sql参数:
    @_p_0 : 1 : System.Boolean,
    @_p_1 : '79c3c002-1474-4b3f-bf83-b17aa173a2bb' : System.Guid,
    @_p_2 : 0 : System.Boolean,
    @_p_3 : 0 : System.Boolean".Trim();
            //var array = Regexs.Split(source, @"(\d{1,2})\.\s");
            var array = Regex.Split(source, @"\d{1,2}\.\s", RegexOptions.None).Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
           Output.WriteLine(array.Count.ToString());
            var result = Format(array);
            Output.WriteLine(result.ToJson());
        }

        private BingLogModel Format(List<string> array)
        {
            var logModel = new BingLogModel();
            FormatLine1(array, logModel);
            return logModel;
        }

        private void FormatLine1(List<string> array, BingLogModel model)
        {
            if (array.Count < 1)
            {
                return;
            }

            var line = array[0];
            var levelAndLogName = line.Split(">>");
            SetLogLevelAndLogName(levelAndLogName[0], model);
            var tempArray = levelAndLogName[1].Split(": ");
            if (tempArray.Length == 4)
            {
                model.TraceId = tempArray[1].Trim();
                model.OperationTime = tempArray[3].Trim();
            }

            if (tempArray.Length == 6)
            {
                model.Duration = tempArray[5].Trim();
            }
        }

        private void SetLogLevelAndLogName(string item,BingLogModel model)
        {
            var firstSplit = Split(item);
            model.Level = firstSplit.Item1;
            model.LogName = firstSplit.Item2;
        }

        private Tuple<string, string> Split(string content)
        {
            var result = content.Trim().Split(": ");
            if (result.Length == 2)
            {
                return new Tuple<string, string>(result[0].Trim(), result[1].Trim());
            }

            if (result.Length == 1)
            {
                return new Tuple<string, string>(result[0].Trim(), string.Empty);
            }

            return null;
        }

        [Fact]
        public void Test_Format()
        {
            var result = $"{1000.01877:0.##}";
            Output.WriteLine(result);
        }


        [Fact]
        public void Test_ToObject()
        {
            var json= "[{\"Text\":\"寸\",\"Value\":\"00000002\",\"SortId\":1,\"Group\":\"尺寸\"}]";
            var items = JsonHelper.ToObject<List<Item>>(json);
            foreach (var item in items)
            {
                Output.WriteLine($"Text: {item.Text}, Value: {item.Value}, SortId: {item.SortId}, Group: {item.Group}, Disabled: {item.Disabled}");
            }
        }

        [Fact]
        public void Test_Tuple_IsNull()
        {
            var list = new List<Tuple<int, decimal>>()
            {
                new Tuple<int, decimal>(1, 20), new Tuple<int, decimal>(2, 20),
            };
            var item = list.FirstOrDefault(x => x.Item1 == 3);
            Assert.Equal(default, item);
        }

    }

    public class BingLogModel
    {
        public int Id { get; set; }
        public DateTime LongDate { get; set; }
        public string Logger { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// 日志名称
        /// </summary>
        public string LogName { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        public string OperationTime { get; set; }

        /// <summary>
        /// 执行时间
        /// </summary>
        public string Duration { get; set; }

        /// <summary>
        /// IP
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 主机
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 线程号
        /// </summary>
        public string ThreadId { get; set; }

        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser { get; set; }

        /// <summary>
        /// 请求地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 业务编号
        /// </summary>
        public string BussinessId { get; set; }

        /// <summary>
        /// 租户
        /// </summary>
        public string Tenant { get; set; }

        /// <summary>
        /// 应用程序
        /// </summary>
        public string Application { get; set; }

        /// <summary>
        /// 模块
        /// </summary>
        public string Module { get; set; }

        /// <summary>
        /// 类名
        /// </summary>
        public string Class { get; set; }

        /// <summary>
        /// 方法
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 参数
        /// </summary>
        public string Params { get; set; }

        /// <summary>
        /// 操作人编号
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 操作人
        /// </summary>
        public string Operator { get; set; }

        /// <summary>
        /// 操作人角色
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Caption { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Sql语句
        /// </summary>
        public string Sql { get; set; }

        /// <summary>
        /// Sql参数
        /// </summary>
        public string SqlParams { get; set; }

        /// <summary>
        /// 错误码
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 异常
        /// </summary>
        public string Exception { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        public int Order { get; set; }
    }
}

