using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Commons {
    /// <summary>
    /// 用户信息查询参数
    /// </summary>
    public class UserInfoQuery : QueryParameter {
        /// <summary>
        /// 用户标识
        /// </summary>
        [Display(Name="用户标识")]
        public Guid? UserId { get; set; }
        
        private string _code = string.Empty;
        /// <summary>
        /// 编码
        /// </summary>
        [Display(Name="编码")]
        public string Code {
            get => _code == null ? string.Empty : _code.Trim();
            set => _code = value;
        }
        
        private string _name = string.Empty;
        /// <summary>
        /// 姓名
        /// </summary>
        [Display(Name="姓名")]
        public string Name {
            get => _name == null ? string.Empty : _name.Trim();
            set => _name = value;
        }
        
        private string _firstName = string.Empty;
        /// <summary>
        /// 名
        /// </summary>
        [Display(Name="名")]
        public string FirstName {
            get => _firstName == null ? string.Empty : _firstName.Trim();
            set => _firstName = value;
        }
        
        private string _lastName = string.Empty;
        /// <summary>
        /// 姓
        /// </summary>
        [Display(Name="姓")]
        public string LastName {
            get => _lastName == null ? string.Empty : _lastName.Trim();
            set => _lastName = value;
        }
        
        private string _nickName = string.Empty;
        /// <summary>
        /// 昵称
        /// </summary>
        [Display(Name="昵称")]
        public string NickName {
            get => _nickName == null ? string.Empty : _nickName.Trim();
            set => _nickName = value;
        }
        
        private string _englishName = string.Empty;
        /// <summary>
        /// 英文名
        /// </summary>
        [Display(Name="英文名")]
        public string EnglishName {
            get => _englishName == null ? string.Empty : _englishName.Trim();
            set => _englishName = value;
        }
        /// <summary>
        /// 性别
        /// </summary>
        [Display(Name="性别")]
        public int? Gender { get; set; }
        /// <summary>
        /// 起始出生日期
        /// </summary>
        [Display( Name = "起始出生日期" )]
        public DateTime? BeginBirthday { get; set; }
        /// <summary>
        /// 结束出生日期
        /// </summary>
        [Display( Name = "结束出生日期" )]
        public DateTime? EndBirthday { get; set; }
        
        private string _nativePlace = string.Empty;
        /// <summary>
        /// 籍贯
        /// </summary>
        [Display(Name="籍贯")]
        public string NativePlace {
            get => _nativePlace == null ? string.Empty : _nativePlace.Trim();
            set => _nativePlace = value;
        }
        /// <summary>
        /// 民族
        /// </summary>
        [Display(Name="民族")]
        public int? Nation { get; set; }
        
        private string _phone = string.Empty;
        /// <summary>
        /// 手机号
        /// </summary>
        [Display(Name="手机号")]
        public string Phone {
            get => _phone == null ? string.Empty : _phone.Trim();
            set => _phone = value;
        }
        
        private string _email = string.Empty;
        /// <summary>
        /// 电子邮件
        /// </summary>
        [Display(Name="电子邮件")]
        public string Email {
            get => _email == null ? string.Empty : _email.Trim();
            set => _email = value;
        }
        
        private string _qq = string.Empty;
        /// <summary>
        /// QQ
        /// </summary>
        [Display(Name="QQ")]
        public string Qq {
            get => _qq == null ? string.Empty : _qq.Trim();
            set => _qq = value;
        }
        
        private string _wechat = string.Empty;
        /// <summary>
        /// 微信
        /// </summary>
        [Display(Name="微信")]
        public string Wechat {
            get => _wechat == null ? string.Empty : _wechat.Trim();
            set => _wechat = value;
        }
        
        private string _fax = string.Empty;
        /// <summary>
        /// 传真
        /// </summary>
        [Display(Name="传真")]
        public string Fax {
            get => _fax == null ? string.Empty : _fax.Trim();
            set => _fax = value;
        }
        /// <summary>
        /// 省份标识
        /// </summary>
        [Display(Name="省份标识")]
        public Guid? ProvinceId { get; set; }
        
        private string _province = string.Empty;
        /// <summary>
        /// 省份
        /// </summary>
        [Display(Name="省份")]
        public string Province {
            get => _province == null ? string.Empty : _province.Trim();
            set => _province = value;
        }
        /// <summary>
        /// 城市标志
        /// </summary>
        [Display(Name="城市标志")]
        public Guid? CityId { get; set; }
        
        private string _city = string.Empty;
        /// <summary>
        /// 城市
        /// </summary>
        [Display(Name="城市")]
        public string City {
            get => _city == null ? string.Empty : _city.Trim();
            set => _city = value;
        }
        /// <summary>
        /// 区县标识
        /// </summary>
        [Display(Name="区县标识")]
        public Guid? CountyId { get; set; }
        
        private string _county = string.Empty;
        /// <summary>
        /// 区县
        /// </summary>
        [Display(Name="区县")]
        public string County {
            get => _county == null ? string.Empty : _county.Trim();
            set => _county = value;
        }
        
        private string _street = string.Empty;
        /// <summary>
        /// 街道
        /// </summary>
        [Display(Name="街道")]
        public string Street {
            get => _street == null ? string.Empty : _street.Trim();
            set => _street = value;
        }
        
        private string _zip = string.Empty;
        /// <summary>
        /// 邮政编码
        /// </summary>
        [Display(Name="邮政编码")]
        public string Zip {
            get => _zip == null ? string.Empty : _zip.Trim();
            set => _zip = value;
        }
        
        private string _idCard = string.Empty;
        /// <summary>
        /// 身份证
        /// </summary>
        [Display(Name="身份证")]
        public string IdCard {
            get => _idCard == null ? string.Empty : _idCard.Trim();
            set => _idCard = value;
        }
        
        private string _avatar = string.Empty;
        /// <summary>
        /// 头像
        /// </summary>
        [Display(Name="头像")]
        public string Avatar {
            get => _avatar == null ? string.Empty : _avatar.Trim();
            set => _avatar = value;
        }
        
        private string _idCardFront = string.Empty;
        /// <summary>
        /// 身份证正面
        /// </summary>
        [Display(Name="身份证正面")]
        public string IdCardFront {
            get => _idCardFront == null ? string.Empty : _idCardFront.Trim();
            set => _idCardFront = value;
        }
        
        private string _idCardReverse = string.Empty;
        /// <summary>
        /// 身份证反面
        /// </summary>
        [Display(Name="身份证反面")]
        public string IdCardReverse {
            get => _idCardReverse == null ? string.Empty : _idCardReverse.Trim();
            set => _idCardReverse = value;
        }
        /// <summary>
        /// 文化程度
        /// </summary>
        [Display(Name="文化程度")]
        public int? Degree { get; set; }
        
        private string _schoolOfGraduate = string.Empty;
        /// <summary>
        /// 毕业学校
        /// </summary>
        [Display(Name="毕业学校")]
        public string SchoolOfGraduate {
            get => _schoolOfGraduate == null ? string.Empty : _schoolOfGraduate.Trim();
            set => _schoolOfGraduate = value;
        }
        
        private string _discipline = string.Empty;
        /// <summary>
        /// 学习专业
        /// </summary>
        [Display(Name="学习专业")]
        public string Discipline {
            get => _discipline == null ? string.Empty : _discipline.Trim();
            set => _discipline = value;
        }
        
        private string _company = string.Empty;
        /// <summary>
        /// 工作单位
        /// </summary>
        [Display(Name="工作单位")]
        public string Company {
            get => _company == null ? string.Empty : _company.Trim();
            set => _company = value;
        }
        
        private string _resume = string.Empty;
        /// <summary>
        /// 个人简历
        /// </summary>
        [Display(Name="个人简历")]
        public string Resume {
            get => _resume == null ? string.Empty : _resume.Trim();
            set => _resume = value;
        }
        
        private string _pinYin = string.Empty;
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Display(Name="拼音简码")]
        public string PinYin {
            get => _pinYin == null ? string.Empty : _pinYin.Trim();
            set => _pinYin = value;
        }
        
        private string _note = string.Empty;
        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name="备注")]
        public string Note {
            get => _note == null ? string.Empty : _note.Trim();
            set => _note = value;
        }
        /// <summary>
        /// 起始创建时间
        /// </summary>
        [Display( Name = "起始创建时间" )]
        public DateTime? BeginCreationTime { get; set; }
        /// <summary>
        /// 结束创建时间
        /// </summary>
        [Display( Name = "结束创建时间" )]
        public DateTime? EndCreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name="创建人")]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 起始最后修改时间
        /// </summary>
        [Display( Name = "起始最后修改时间" )]
        public DateTime? BeginLastModificationTime { get; set; }
        /// <summary>
        /// 结束最后修改时间
        /// </summary>
        [Display( Name = "结束最后修改时间" )]
        public DateTime? EndLastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display(Name="最后修改人")]
        public Guid? LastModifierId { get; set; }
        
        private string _tenantId = string.Empty;
        /// <summary>
        /// 租户标识
        /// </summary>
        [Display(Name="租户标识")]
        public string TenantId {
            get => _tenantId == null ? string.Empty : _tenantId.Trim();
            set => _tenantId = value;
        }
    }
}