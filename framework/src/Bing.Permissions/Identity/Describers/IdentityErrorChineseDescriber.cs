using Bing.Permissions.Properties;
using Microsoft.AspNetCore.Identity;

namespace Bing.Permissions.Identity.Describers;

/// <summary>
/// Identity中文错误描述
/// </summary>
public class IdentityErrorChineseDescriber : IdentityErrorDescriber
{
    #region PasswordTooShort(密码太短)

    /// <summary>
    /// 密码太短
    /// </summary>
    /// <param name="length">密码长度</param>
    /// <returns>描述密码长度不符合要求的 Identity 错误。</returns>
    public override IdentityError PasswordTooShort(int length) =>
        new IdentityError
        {
            Code = nameof(PasswordTooShort),
            Description = string.Format(SecurityResources.PasswordTooShort, length)
        };

    #endregion

    #region PasswordRequiresNonAlphanumeric(密码应包含非字母和数字的特殊字符)

    /// <summary>
    /// 密码应包含非字母和数字的特殊字符
    /// </summary>
    /// <returns>描述密码缺少非字母数字字符的 Identity 错误。</returns>
    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new IdentityError
        {
            Code = nameof(PasswordRequiresNonAlphanumeric),
            Description = string.Format(SecurityResources.PasswordRequiresNonAlphanumeric)
        };

    #endregion

    #region PasswordRequiresUpper(密码应包含大写字母)

    /// <summary>
    /// 密码应包含大写字母
    /// </summary>
    /// <returns>描述密码缺少大写字母的 Identity 错误。</returns>
    public override IdentityError PasswordRequiresUpper() =>
        new IdentityError
        {
            Code = nameof(PasswordRequiresUpper),
            Description = SecurityResources.PasswordRequiresUpper
        };

    #endregion

    #region PasswordRequiresLower(密码应包含小写字母)

    /// <summary>
    /// 密码应包含小写字母
    /// </summary>
    /// <returns>描述密码缺少小写字母的 Identity 错误。</returns>
    public override IdentityError PasswordRequiresLower() =>
        new IdentityError
        {
            Code = nameof(PasswordRequiresLower),
            Description = SecurityResources.PasswordRequiresLower
        };

    #endregion

    #region PasswordRequiresDigit(密码应包含数字)

    /// <summary>
    /// 密码应包含数字
    /// </summary>
    /// <returns>描述密码缺少数字的 Identity 错误。</returns>
    public override IdentityError PasswordRequiresDigit() =>
        new IdentityError
        {
            Code = nameof(PasswordRequiresDigit),
            Description = SecurityResources.PasswordRequiresDigit
        };

    #endregion

    #region PasswordRequiresUniqueChars(密码应包含不重复的字符数)

    /// <summary>
    /// 密码应包含不重复的字符数
    /// </summary>
    /// <param name="uniqueChars">重复字符数</param>
    /// <returns>描述密码唯一字符数不符合要求的 Identity 错误。</returns>
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new IdentityError
        {
            Code = nameof(PasswordRequiresUniqueChars),
            Description = string.Format(SecurityResources.PasswordRequiresUniqueChars, uniqueChars)
        };

    #endregion

    #region InvalidUserName(无效用户名)

    /// <summary>
    /// 无效用户名
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <returns>描述用户名无效的 Identity 错误。</returns>
    public override IdentityError InvalidUserName(string userName) =>
        new IdentityError
        {
            Code = nameof(InvalidUserName),
            Description = string.Format(SecurityResources.InvalidUserName, userName)
        };

    #endregion

    #region DuplicateUserName(用户名重复)

    /// <summary>
    /// 用户名重复
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <returns>描述用户名重复的 Identity 错误。</returns>
    public override IdentityError DuplicateUserName(string userName) =>
        new IdentityError
        {
            Code = nameof(DuplicateUserName),
            Description = string.Format(SecurityResources.DuplicateUserName, userName)
        };

    #endregion

    #region DuplicateEmail(电子邮件重复)

    /// <summary>
    /// 电子邮件重复
    /// </summary>
    /// <param name="email">电子邮件</param>
    /// <returns>描述电子邮件重复的 Identity 错误。</returns>
    public override IdentityError DuplicateEmail(string email) =>
        new IdentityError
        {
            Code = nameof(DuplicateEmail),
            Description = string.Format(SecurityResources.DuplicateEmail, email)
        };

    #endregion

    #region InvalidEmail(无效电子邮件)

    /// <summary>
    /// 无效电子邮件
    /// </summary>
    /// <param name="email">电子邮件</param>
    /// <returns>描述电子邮件无效的 Identity 错误。</returns>
    public override IdentityError InvalidEmail(string email) =>
        new IdentityError
        {
            Code = nameof(InvalidEmail),
            Description = string.Format(SecurityResources.InvalidEmail, email)
        };

    #endregion

    #region InvalidToken(无效令牌)

    /// <summary>
    /// 无效令牌
    /// </summary>
    /// <returns>描述令牌无效的 Identity 错误。</returns>
    public override IdentityError InvalidToken() =>
        new IdentityError
        {
            Code = nameof(InvalidToken),
            Description = SecurityResources.InvalidToken
        };

    #endregion

    #region PasswordMismatch(密码错误)

    /// <summary>
    /// 密码错误
    /// </summary>
    /// <returns>描述密码不匹配的 Identity 错误。</returns>
    public override IdentityError PasswordMismatch() =>
        new IdentityError
        {
            Code = nameof(PasswordMismatch),
            Description = SecurityResources.PasswordMismatch
        };

    #endregion

    #region InvalidRoleName(角色名无效)

    /// <summary>
    /// 角色名无效
    /// </summary>
    /// <param name="role">角色名</param>
    /// <returns>描述角色名称无效的 Identity 错误。</returns>
    public override IdentityError InvalidRoleName(string role) =>
        new IdentityError
        {
            Code = nameof(InvalidRoleName),
            Description = string.Format(SecurityResources.InvalidRoleName, role)
        };

    #endregion

    #region DuplicateRoleName(角色名重复)

    /// <summary>
    /// 角色名重复
    /// </summary>
    /// <param name="role">角色名</param>
    /// <returns>描述角色名称重复的 Identity 错误。</returns>
    public override IdentityError DuplicateRoleName(string role) =>
        new IdentityError
        {
            Code = nameof(DuplicateRoleName),
            Description = string.Format(SecurityResources.DuplicateRoleName, role)
        };

    #endregion

    #region UserNotInRole(用户未包含角色)

    /// <summary>
    /// 用户未包含角色
    /// </summary>
    /// <param name="role">角色名</param>
    /// <returns>描述用户不属于指定角色的 Identity 错误。</returns>
    public override IdentityError UserNotInRole(string role) =>
        new IdentityError()
        {
            Code = nameof(UserNotInRole),
            Description = string.Format(SecurityResources.UserNotInRole, role)
        };

    #endregion
}