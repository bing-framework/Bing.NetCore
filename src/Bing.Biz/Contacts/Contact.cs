using System.ComponentModel.DataAnnotations.Schema;
using Bing.Domains.Values;

namespace Bing.Biz.Contacts
{
    /// <summary>
    /// 联系 -不可变
    /// </summary>
    public class Contact : ValueObjectBase<Contact>
    {
        /// <summary>
        /// 电话
        /// </summary>
        [Column("Phone")]
        public string Phone { get; private set; }

        /// <summary>
        /// 手机
        /// </summary>
        [Column("Mobile")]
        public string Mobile { get; private set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        [Column("Email")]
        public string Email { get; private set; }

        /// <summary>
        /// QQ
        /// </summary>
        [Column("QQ")]
        // ReSharper disable once InconsistentNaming
        public string QQ { get; private set; }

        /// <summary>
        /// MSN
        /// </summary>
        [Column("MSN")]
        // ReSharper disable once InconsistentNaming
        public string MSN { get; private set; }

        /// <summary>
        /// 微信
        /// </summary>
        [Column("WeChat")]
        public string WeChat { get; private set; }

        /// <summary>
        /// 微博
        /// </summary>
        [Column("WeiBo")]
        public string WeiBo { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="Contact"/>类型的实例
        /// </summary>
        public Contact() { }

        /// <summary>
        /// 初始化一个<see cref="Contact"/>类型的实例
        /// </summary>
        /// <param name="phone">电话</param>
        /// <param name="mobile">手机</param>
        /// <param name="email">邮箱</param>
        /// <param name="qq">QQ</param>
        /// <param name="msn">MSN</param>
        /// <param name="weChat">微信</param>
        /// <param name="weiBo">微博</param>
        public Contact(string phone, string mobile, string email, string qq, string msn, string weChat, string weiBo)
        {
            Phone = phone;
            Mobile = mobile;
            Email = email;
            QQ = qq;
            MSN = msn;
            WeChat = weChat;
            WeiBo = weiBo;
        }

        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription("电话", Phone);
            AddDescription("手机", Mobile);
            AddDescription("Email", Email);
            AddDescription("QQ", QQ);
            AddDescription("MSN", MSN);
            AddDescription("微信", WeChat);
            AddDescription("微博", WeiBo);
        }
    }
}
