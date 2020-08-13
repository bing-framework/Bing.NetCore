using Bing.Admin.Infrastructure;
using Bing.Events.Messages;

namespace Bing.Admin.Systems.Domain.Events
{
    /// <summary>
    /// 测试消息事件
    /// </summary>
    public class TestMessageEvent1 : MessageEvent<TestMessage>
    {
        /// <summary>
        /// 初始化一个<see cref="TestMessageEvent1"/>类型的实例
        /// </summary>
        /// <param name="data">数据</param>
        public TestMessageEvent1(TestMessage data) : base(data)
        {
            Send = false;
            Name = MessageEventConst.TestMessage1;
        }
    }

    /// <summary>
    /// 测试消息事件
    /// </summary>
    public class TestMessageEvent2 : MessageEvent<TestMessage>
    {
        /// <summary>
        /// 初始化一个<see cref="TestMessageEvent2"/>类型的实例
        /// </summary>
        /// <param name="data">数据</param>
        public TestMessageEvent2(TestMessage data) : base(data)
        {
            Send = false;
            Name = MessageEventConst.TestMessage2;
        }
    }

    /// <summary>
    /// 测试消息
    /// </summary>
    public class TestMessage
    {
        /// <summary>
        /// 标识
        /// </summary>
        public string Id { get; set; }
    }
}
