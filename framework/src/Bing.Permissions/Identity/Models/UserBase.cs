using System.ComponentModel.DataAnnotations.Schema;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Security.Encryption;
using Bing.Validation;

namespace Bing.Permissions.Identity.Models;

/// <summary>
/// 用户基类
/// </summary>
public partial class UserBase<TUser, TKey>
{
    /// <summary>
    /// 获取或设置用于处理用户密码的加密器；该属性不映射到数据库。
    /// </summary>
    [NotMapped]
    public IEncryptor Encryptor { get; set; }

    #region Init(初始化)

    /// <summary>
    /// 初始化用户实体，并在必要时根据手机号或邮箱补充用户名。
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
    /// 验证用户实体；用户名为空时先抛出验证警告。
    /// </summary>
    /// <returns>验证成功时返回基类生成的验证结果。</returns>
    public override IValidationResult Validate()
    {
        if (UserName.IsEmpty())
            throw new Warning(Bing.Permissions.Properties.SecurityResources.UserNameIsEmpty);
        return base.Validate();
    }

    #endregion

    #region SetPassword(设置密码)

    /// <summary>
    /// 根据配置保存加密后的原始密码，或清空原始密码字段。
    /// </summary>
    /// <param name="password">待处理的原始密码。</param>
    /// <param name="storeOriginalPassword">是否保存加密后的原始密码。</param>
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
    /// <returns>当前用户使用的加密器。</returns>
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
    /// <returns>解密后的用户密码。</returns>
    public string GetPassword() => GetEncryptor().Decrypt(Password);

    #endregion

    #region GetSafePassword(获取安全码)

    /// <summary>
    /// 获取安全码
    /// </summary>
    /// <returns>解密后的用户安全码。</returns>
    public string GetSafePassword() => GetEncryptor().Decrypt(SafePassword);

    #endregion
}