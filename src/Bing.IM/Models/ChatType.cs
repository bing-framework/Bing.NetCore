using System.ComponentModel;

namespace Bing.IM.Models
{
    /// <summary>
    /// 聊天类型
    /// </summary>
    public enum ChatType
    {
        /// <summary>
        /// 私聊
        /// </summary>
        [Description("私聊")]
        Friend = 1,

        /// <summary>
        /// 群聊
        /// </summary>
        [Description("群聊")]
        Group = 2,

        /// <summary>
        /// 客服
        /// </summary>
        [Description("客服")]
        CustomerService = 3
    }

    /// <summary>
    /// 聊天类型扩展
    /// </summary>
    public static class ChatTypeExtensions
    {
        /// <summary>
        /// 转换成字符串
        /// </summary>
        /// <param name="chatType">聊天类型</param>
        /// <returns></returns>
        public static string ToChatTypeString(this ChatType chatType)
        {
            switch (chatType)
            {
                case ChatType.Friend:
                    return "friend";

                case ChatType.Group:
                    return "group";

                case ChatType.CustomerService:
                    return "kefu";
            }
            return null;
        }
    }
}
