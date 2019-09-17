using System.ComponentModel.DataAnnotations.Schema;
using Bing.Exceptions;
using Bing.Security.Encryptors;
using Bing.Utils.Extensions;
using Bing.Validations;

namespace Bing.Permissions.Identity.Models
{
    /// <summary>
    /// 用户基类
    /// </summary>
    public partial class UserBase<TUser, TKey>
    {
        /// <summary>
        /// 加密器
        /// </summary>
        [NotMapped]
        public IEncryptor Encryptor { get; set; }

        #region Init(初始化)

        /// <summary>
        /// 初始化
        /// </summary>
        public override void Init()
        {
            base.Init();
            InitUserName();
        }

        /// <summary>
        /// 初始化用户名
        /// </summary>
        private void InitUserName()
        {
            if (UserName.IsEmpty() == false)
                return;
            if (PhoneNumber.IsEmpty() == false)
            {
                UserName = PhoneNumber;
                return;
            }
            if (Email.IsEmpty() == false)
                UserName = Email;
        }

        #endregion

        #region Validate(验证)

        /// <summary>
        /// 验证
        /// </summary>
        public override ValidationResultCollection Validate()
        {
            if (UserName.IsEmpty())
                throw new Warning(Bing.Permissions.Properties.SecurityResources.UserNameIsEmpty);
            return base.Validate();
        }

        #endregion

        #region SetPassword(设置密码)

        /// <summary>
        /// 设置密码
        /// </summary>
        /// <param name="password">密码</param>
        /// <param name="storeOriginalPassword">是否存储原始密码</param>
        public void SetPassword(string password, bool? storeOriginalPassword)
        {
            if (storeOriginalPassword.SafeValue())
            {
                Password = GetEncryptor().Encrypt(password);
                return;
            }
            Password = null;
        }

        #endregion

        #region GetEncryptor(获取加密器)

        /// <summary>
        /// 获取加密器
        /// </summary>
        protected virtual IEncryptor GetEncryptor() => Encryptor ?? NullEncryptor.Instance;

        #endregion

        #region SetSafePassword(设置安全码)

        /// <summary>
        /// 设置安全码
        /// </summary>
        /// <param name="password">安全码</param>
        /// <param name="storeOriginalPassword">是否存储原始密码</param>
        public void SetSafePassword(string password, bool? storeOriginalPassword)
        {
            if (storeOriginalPassword.SafeValue())
            {
                SafePassword = GetEncryptor().Encrypt(password);
                return;
            }
            SafePassword = null;
        }

        #endregion

        #region GetPassword(获取密码)

        /// <summary>
        /// 获取密码
        /// </summary>
        public string GetPassword() => GetEncryptor().Decrypt(Password);

        #endregion

        #region GetSafePassword(获取安全码)

        /// <summary>
        /// 获取安全码
        /// </summary>
        public string GetSafePassword() => GetEncryptor().Decrypt(SafePassword);

        #endregion
    }
}
