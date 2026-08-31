using Bing.Domain.Services;
using Bing.Permissions.Identity.Models;

namespace Bing.Permissions.Identity.Services.Abstractions;

/// <summary>
/// 用户管理s
/// </summary>
/// <typeparam name="TUser">用户类型个</typeparam>
/// <typeparam name="TKey">用户标识类型</typeparam>
public interface IUserManager<TUser, TKey> : IDomainService where TUser : UserBase<TUser, TKey>
{
    /// <summary>
    /// 创建用户
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="password">密码</param>
    Task CreateAsync(TUser user, string password);

    /// <summary>
    /// 生成令牌
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <param name="purpose">用途</param>
    /// <param name="application">应用程序</param>
    /// <param name="provider">令牌提供器</param>
    /// <returns>表示生成令牌结果的异步操作。</returns>
    Task<string> GenerateTokenAsync(string phone, string purpose, string application = "", string provider = "");

    /// <summary>
    /// 生成令牌
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="purpose">用途</param>
    /// <param name="application">应用程序</param>
    /// <param name="provider">令牌提供器</param>
    /// <returns>表示生成令牌结果的异步操作。</returns>
    Task<string> GenerateTokenAsync(TUser user, string purpose, string application = "", string provider = "");

    /// <summary>
    /// 验证令牌
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <param name="purpose">用途</param>
    /// <param name="token">令牌</param>
    /// <param name="application">应用程序</param>
    /// <param name="provider">令牌提供器</param>
    /// <returns>表示令牌验证结果的异步操作。</returns>
    Task<bool> VerifyTokenAsync(string phone, string purpose, string token, string application = "",
        string provider = "");

    /// <summary>
    /// 验证令牌
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="purpose">用途</param>
    /// <param name="token">令牌</param>
    /// <param name="application">应用程序</param>
    /// <param name="provider">令牌提供器</param>
    /// <returns>表示令牌验证结果的异步操作。</returns>
    Task<bool> VerifyTokenAsync(TUser user, string purpose, string token, string application = "",
        string provider = "");

    /// <summary>
    /// 设置手机号
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="phone">手机号</param>
    Task SetPhoneNumberAsync(TUser user, string phone);

    /// <summary>
    /// 生成手机号注册令牌
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <param name="application">应用程序</param>
    /// <returns>表示生成注册令牌结果的异步操作。</returns>
    Task<string> GenerateRegisterTokenAsync(string phone, string application = "");

    /// <summary>
    /// 验证手机号注册令牌
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <param name="token">令牌</param>
    /// <param name="application">应用程序</param>
    /// <returns>表示注册令牌验证结果的异步操作。</returns>
    Task<bool> VerifyRegisterTokenAsync(string phone, string token, string application = "");

    /// <summary>
    /// 生成电子邮件确认令牌
    /// </summary>
    /// <param name="user">用户</param>
    /// <returns>表示生成电子邮件确认令牌结果的异步操作。</returns>
    Task<string> GenerateEmailConfirmationTokenAsync(TUser user);

    /// <summary>
    /// 设置电子邮件
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="email">电子邮件</param>
    Task SetEmailAsync(TUser user, string email);

    /// <summary>
    /// 激活电子邮件
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="token">令牌</param>
    Task ConfirmEmailAsync(TUser user, string token);

    /// <summary>
    /// 生成电子邮件重置密码令牌
    /// </summary>
    /// <param name="user">用户</param>
    /// <returns>表示生成电子邮件重置密码令牌结果的异步操作。</returns>
    Task<string> GenerateEmailPasswordResetTokenAsync(TUser user);

    /// <summary>
    /// 通过电子邮件重置密码
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="token">令牌</param>
    /// <param name="newPassword">新密码</param>
    Task ResetPasswordByEmailAsync(TUser user, string token, string newPassword);

    /// <summary>
    /// 生成手机号重置密码令牌
    /// </summary>
    /// <param name="user">用户</param>
    /// <returns>表示生成手机号重置密码令牌结果的异步操作。</returns>
    Task<string> GeneratePhonePasswordResetTokenAsync(TUser user);

    /// <summary>
    /// 通过手机号重置密码
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="token">令牌</param>
    /// <param name="newPassword">新密码</param>
    Task ResetPasswordByPhoneAsync(TUser user, string token, string newPassword);

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="currentPassword">当前密码</param>
    /// <param name="newPassword">新密码</param>
    Task ChangePasswordAsync(TUser user, string currentPassword, string newPassword);

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="newPassword">新密码</param>
    Task ChangePasswordAsync(TUser user, string newPassword);

    /// <summary>
    /// 通过用户名查找
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <returns>表示用户查询结果的异步操作；未找到时结果为 null。</returns>
    Task<TUser> FindByNameAsync(string userName);

    /// <summary>
    /// 通过电子邮件查找
    /// </summary>
    /// <param name="email">电子邮件</param>
    /// <returns>表示用户查询结果的异步操作；未找到时结果为 null。</returns>
    Task<TUser> FindByEmailAsync(string email);

    /// <summary>
    /// 通过手机号查找
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <returns>表示用户查询结果的异步操作；未找到时结果为 null。</returns>
    Task<TUser> FindByPhoneAsync(string phone);
}