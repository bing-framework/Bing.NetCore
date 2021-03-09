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
        /// <param name="send">是否自动发送</param>
        public TestMessageEvent1(TestMessage data, bool send = false) : base(data)
        {
            Send = send;
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
        /// <param name="send">是否自动发送</param>
        public TestMessageEvent2(TestMessage data, bool send = false) : base(data)
        {
            Send = send;
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

        /// <summary>
        /// 是否自动发送
        /// </summary>
        public bool Send { get; set; }

        /// <summary>
        /// 是否抛异常
        /// </summary>
        public bool ThrowException { get; set; }

        /// <summary>
        /// 是否需要提交
        /// </summary>
        public bool NeedCommit { get; set; }
    }
}
