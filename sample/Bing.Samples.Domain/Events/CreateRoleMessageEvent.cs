using Bing.Events.Messages;

namespace Bing.Samples.Domain.Events
{
    /// <summary>
    /// 创建角色消息事件
    /// </summary>
    public class CreateRoleMessageEvent : MessageEvent
    {
        /// <summary>
        /// 初始化一个<see cref="CreateRoleMessageEvent"/>类型的实例
        /// </summary>
        /// <param name="data">数据</param>
        public CreateRoleMessageEvent(CreateRoleMessage data)
        {
            Name = "CreateRole";
            Data = data;
            Send = false;
        }
    }

    /// <summary>
    /// 创建角色消息
    /// </summary>
    public class CreateRoleMessage
    {
        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 类型
        /// </summary>
        public string Type { get; set; }
    }
}
