using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Develops;
using Bing.Utils.IdGenerators.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.IdGenerators
{
    public class ObjectIdGeneratorTest:TestBase
    {
        public ObjectIdGeneratorTest(ITestOutputHelper output) : base(output)
        {
            CodeTimer.Initialize();
        }

        [Fact]
        public void Test_Create()
        {
            CodeTimer.CodeExecuteTime(() =>
            {
                var result = ObjectIdGenerator.Current.Create();
                Output.WriteLine(result.ToString());
            });
        }

        [Fact]
        public void Test_Create_100()
        {
            CodeTimer.CodeExecuteTime(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var result = ObjectIdGenerator.Current.Create();
                    Output.WriteLine(result.ToString());
                }
            });
        }

        [Fact]
        public void Test_Create_1000()
        {
            CodeTimer.CodeExecuteTime(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var result = ObjectIdGenerator.Current.Create();
                    Output.WriteLine(result.ToString());
                }
            });
        }

        [Fact]
        public void Test_Create_10000()
        {
            CodeTimer.CodeExecuteTime(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var result = ObjectIdGenerator.Current.Create();
                    Output.WriteLine(result.ToString());
                }
            });
        }

        [Fact]
        public void Test_Create_10W()
        {
            Create(100000);
        }

        [Fact]
        public void Test_Create_Thread10_100W()
        {
            UnitTester.TestConcurrency(() =>
            {
                Create(1000000);
            }, 10);
            Output.WriteLine("数量：" + _set.Count);
        }

        private static object _lock = new object();

        private static HashSet<string> _set = new HashSet<string>();

        private void Create(long length)
        {
            CodeTimer.CodeExecuteTime(() =>
            {
                for (int i = 0; i < length; i++)
                {
                    var result = ObjectIdGenerator.Current.Create();
                    lock (_lock)
                    {
                        if (_set.Contains(result))
                        {
                            Output.WriteLine("发现重复项：{0}", result);
                        }
                        else
                        {
                            _set.Add(result);
                        }
                    }
                }
            });
        }
    }
}
