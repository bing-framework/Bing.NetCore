using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Bing.Extensions.AutoMapper.Tests.Samples;
using Xunit;

namespace Bing.Extensions.AutoMapper.Tests
{
    /// <summary>
    /// AutoMapper 对象映射测试
    /// </summary>
    public class MapTest
    {
        /// <summary>
        /// 测试映射
        /// </summary>
        [Fact]
        public void Test_MapTo()
        {
            Sample sample=new Sample();
            Sample2 sample2=new Sample2(){StringValue = "a"};
            sample2.MapTo(sample);

            Assert.Equal("a",sample.StringValue);
        }

        /// <summary>
        /// 测试映射
        /// </summary>
        [Fact]
        public void Test_MapTo_2()
        {
            Sample sample=new Sample(){StringValue = "a"};
            Sample2 sample2 = sample.MapTo<Sample2>();

            Assert.Equal("a",sample2.StringValue);
        }

        /// <summary>
        /// 测试映射集合
        /// </summary>
        [Fact]
        public void Test_MapTo_List()
        {
            List<Sample> sampleList=new List<Sample>(){new Sample(){StringValue = "a"},new Sample(){StringValue = "b"}};
            List<Sample2> sample2List = new List<Sample2>();
            sampleList.MapTo(sample2List);

            Assert.Equal(2,sample2List.Count);
            Assert.Equal("a",sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试映射集合
        /// </summary>
        [Fact]
        public void Test_MapTo_List_2()
        {
            List<Sample> sampleList = new List<Sample>() { new Sample() { StringValue = "a" }, new Sample() { StringValue = "b" } };
            List<Sample2> sample2List = sampleList.MapTo<List<Sample2>>();

            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试映射集合
        /// </summary>
        [Fact]
        public void Test_MapToList()
        {
            List<Sample> sampleList = new List<Sample>() { new Sample() { StringValue = "a" }, new Sample() { StringValue = "b" } };
            List<Sample2> sample2List = sampleList.MapToList<Sample2>();

            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 映射集合 - 测试空集合
        /// </summary>
        [Fact]
        public void Test_MapToList_Empty()
        {
            List<Sample> sampleList=new List<Sample>();
            List<Sample2> sample2List = sampleList.MapToList<Sample2>();

            Assert.Empty(sample2List);
        }

        /// <summary>
        /// 映射集合 - 测试数组
        /// </summary>
        [Fact]
        public void Test_MapToList_Array()
        {
            Sample[] sampleList = new Sample[] {new Sample() {StringValue = "a"}, new Sample() {StringValue = "b"}};
            List<Sample2> sample2List = sampleList.MapToList<Sample2>();

            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        public void Test_MapTo_MultipleThread()
        {
            
        }
    }
}
