using Bing.Security.Properties;
using Microsoft.AspNetCore.Identity;

namespace Bing.Security.Identity.Describers
{
    /// <summary>
    /// Identity中文错误描述
    /// </summary>
    public class IdentityErrorChineseDescriber:IdentityErrorDescriber
    {
        /// <summary>
        /// 密码太短
        /// </summary>
        /// <param name="length">密码长度</param>
        /// <returns></returns>
        public override IdentityError PasswordTooShort(int length)
        {
            return new IdentityError()
            {
                Code = nameof(PasswordTooShort),
                Description = string.Format(SecurityResources.PasswordTooShort, length)
            };
        }

        /// <summary>
        /// 密码应包含非字母和数字的特殊字符
        /// </summary>
        /// <returns></returns>
        public override IdentityError PasswordRequiresNonAlphanumeric()
        {
            return new IdentityError()
            {
                Code = nameof(PasswordRequiresNonAlphanumeric),
                Description = string.Format(SecurityResources.PasswordRequiresNonAlphanumeric)
            };
        }

        /// <summary>
        /// 密码应包含大写字母
        /// </summary>
        /// <returns></returns>
        public override IdentityError PasswordRequiresUpper()
        {
            return new IdentityError()
            {
                Code = nameof(PasswordRequiresUpper),
                Description = SecurityResources.PasswordRequiresUpper
            };
        }

        /// <summary>
        /// 密码应包含小写字母
        /// </summary>
        /// <returns></returns>
        public override IdentityError PasswordRequiresLower()
        {
            return new IdentityError()
            {
                Code = nameof(PasswordRequiresLower),
                Description = SecurityResources.PasswordRequiresLower
            };
        }

        /// <summary>
        /// 密码应包含数字
        /// </summary>
        /// <returns></returns>
        public override IdentityError PasswordRequiresDigit()
        {
            return new IdentityError()
            {
                Code = nameof(PasswordRequiresDigit),
                Description = SecurityResources.PasswordRequiresDigit
            };
        }

        /// <summary>
        /// 密码应包含不重复的字符数
        /// </summary>
        /// <param name="uniqueChars">重复字符数</param>
        /// <returns></returns>
        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        {
            return new IdentityError()
            {
                Code = nameof(PasswordRequiresUniqueChars),
                Description = string.Format(SecurityResources.PasswordRequiresUniqueChars,uniqueChars)
            };
        }

        /// <summary>
        /// 无效用户名
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <returns></returns>
        public override IdentityError InvalidUserName(string userName)
        {
            return new IdentityError()
            {
                Code = nameof(InvalidUserName),
                Description = string.Format(SecurityResources.InvalidUserName,userName)
            };
        }

        /// <summary>
        /// 用户名重复
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <returns></returns>
        public override IdentityError DuplicateUserName(string userName)
        {
            return new IdentityError()
            {
                Code = nameof(DuplicateUserName),
                Description = string.Format(SecurityResources.DuplicateUserName,userName)
            };
        }

        /// <summary>
        /// 电子邮件重复
        /// </summary>
        /// <param name="email">电子邮件</param>
        /// <returns></returns>
        public override IdentityError DuplicateEmail(string email)
        {
            return new IdentityError()
            {
                Code = nameof(DuplicateEmail),
                Description = string.Format(SecurityResources.DuplicateEmail,email)
            };
        }

        /// <summary>
        /// 无效电子邮件
        /// </summary>
        /// <param name="email">电子邮件</param>
        /// <returns></returns>
        public override IdentityError InvalidEmail(string email)
        {
            return new IdentityError()
            {
                Code = nameof(InvalidEmail),
                Description = string.Format(SecurityResources.InvalidEmail,email)
            };
        }

        /// <summary>
        /// 无效令牌
        /// </summary>
        /// <returns></returns>
        public override IdentityError InvalidToken()
        {
            return new IdentityError()
            {
                Code = nameof(InvalidToken),
                Description = SecurityResources.InvalidToken
            };
        }

        /// <summary>
        /// 密码错误
        /// </summary>
        /// <returns></returns>
        public override IdentityError PasswordMismatch()
        {
            return new IdentityError()
            {
                Code = nameof(PasswordMismatch),
                Description = SecurityResources.PasswordMismatch
            };
        }

        /// <summary>
        /// 角色名无效
        /// </summary>
        /// <param name="role">角色名</param>
        /// <returns></returns>
        public override IdentityError InvalidRoleName(string role)
        {
            return new IdentityError()
            {
                Code = nameof(InvalidRoleName),
                Description = string.Format(SecurityResources.InvalidRoleName,role)
            };
        }

        /// <summary>
        /// 角色名重复
        /// </summary>
        /// <param name="role">角色名</param>
        /// <returns></returns>
        public override IdentityError DuplicateRoleName(string role)
        {
            return new IdentityError()
            {
                Code = nameof(DuplicateRoleName),
                Description = string.Format(SecurityResources.DuplicateRoleName,role)
            };
        }

        
    }
}
