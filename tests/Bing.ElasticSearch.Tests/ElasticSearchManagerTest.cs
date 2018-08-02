using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.ElasticSearch.Tests.Samples;
using Bing.Utils.Develops;
using Xunit;
using Xunit.Abstractions;

namespace Bing.ElasticSearch.Tests
{
    public class ElasticSearchManagerTest:TestBase
    {
        private IElasticSearchManager _manager;
        public ElasticSearchManagerTest(ITestOutputHelper output) : base(output)
        {
            _manager = CreateManager();
        }

        /// <summary>
        /// 创建索引。不映射
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_CreateIndex_NotMap()
        {
            await _manager.CreateIndexAsync("test");
        }

        /// <summary>
        /// 创建索引。自动映射实体属性
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_CreateIndex_Map()
        {
            await _manager.CreateIndexAsync<Customer>("customer");
        }

        /// <summary>
        /// 删除索引
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_DeleteIndex()
        {
            await _manager.DeleteIndexAsync("test636688467634000629");
        }

        /// <summary>
        /// 保存。映射
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_Save_Map()
        {
            var customer=new Customer()
            {
                Id = Guid.NewGuid(),
                Name = "隔壁老王",
                Age = 18
            };
            await _manager.SaveAsync("customer", customer);
        }

        /// <summary>
        /// 批量保存。映射
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_BatchSave_Map()
        {
            var list = new List<Customer>();
            var random=new Random(100);
            for (var i = 0; i < 10000; i++)
            {
                list.Add(new Customer()
                {
                    Id = Guid.NewGuid(),
                    Age = random.Next(1,100),
                    Name = $"隔壁老王{i+random.Next(0,random.Next(0,99999))}"
                });
            }

            await _manager.BulkSaveAsync("customer", list, list.Count);
        }
    }
}
