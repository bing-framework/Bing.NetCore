using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Utils;
using Bing.Domains;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Domains.Entities.Tenants;

namespace Bing.DbDesigner.Commons.Domain.Models {
    /// <summary>
    /// 用户信息
    /// </summary>
    [DisplayName( "用户信息" )]
    public partial class UserInfo : AggregateRoot<UserInfo>,ITenant,IDelete,IAudited {
        /// <summary>
        /// 初始化用户信息
        /// </summary>
        public UserInfo() : this( Guid.Empty ) {
        }

        /// <summary>
        /// 初始化用户信息
        /// </summary>
        /// <param name="id">用户信息标识</param>
        public UserInfo( Guid id ) : base( id ) {
        }

        /// <summary>
        /// 编码
        /// </summary>
        [DisplayName( "编码" )]
        [StringLength( 100, ErrorMessage = "编码输入过长，不能超过100位" )]
        public string Code { get; set; }
        /// <summary>
        /// 姓名
        /// </summary>
        [DisplayName( "姓名" )]
        [StringLength( 200, ErrorMessage = "姓名输入过长，不能超过200位" )]
        public string Name { get; set; }
        /// <summary>
        /// 名
        /// </summary>
        [DisplayName( "名" )]
        [StringLength( 200, ErrorMessage = "名输入过长，不能超过200位" )]
        public string FirstName { get; set; }
        /// <summary>
        /// 姓
        /// </summary>
        [DisplayName( "姓" )]
        [StringLength( 200, ErrorMessage = "姓输入过长，不能超过200位" )]
        public string LastName { get; set; }
        /// <summary>
        /// 昵称
        /// </summary>
        [DisplayName( "昵称" )]
        [StringLength( 50, ErrorMessage = "昵称输入过长，不能超过50位" )]
        public string NickName { get; set; }
        /// <summary>
        /// 英文名
        /// </summary>
        [DisplayName( "英文名" )]
        [StringLength( 200, ErrorMessage = "英文名输入过长，不能超过200位" )]
        public string EnglishName { get; set; }
        /// <summary>
        /// 性别
        /// </summary>
        [DisplayName( "性别" )]
        public int? Gender { get; set; }
        /// <summary>
        /// 出生日期
        /// </summary>
        [DisplayName( "出生日期" )]
        public DateTime? Birthday { get; set; }
        /// <summary>
        /// 籍贯
        /// </summary>
        [DisplayName( "籍贯" )]
        [StringLength( 50, ErrorMessage = "籍贯输入过长，不能超过50位" )]
        public string NativePlace { get; set; }
        /// <summary>
        /// 民族
        /// </summary>
        [DisplayName( "民族" )]
        public int? Nation { get; set; }
        /// <summary>
        /// 手机号
        /// </summary>
        [DisplayName( "手机号" )]
        [StringLength( 50, ErrorMessage = "手机号输入过长，不能超过50位" )]
        public string Phone { get; set; }
        /// <summary>
        /// 电子邮件
        /// </summary>
        [DisplayName( "电子邮件" )]
        [StringLength( 500, ErrorMessage = "电子邮件输入过长，不能超过500位" )]
        public string Email { get; set; }
        /// <summary>
        /// QQ
        /// </summary>
        [DisplayName( "QQ" )]
        [StringLength( 20, ErrorMessage = "QQ输入过长，不能超过20位" )]
        public string Qq { get; set; }
        /// <summary>
        /// 微信
        /// </summary>
        [DisplayName( "微信" )]
        [StringLength( 50, ErrorMessage = "微信输入过长，不能超过50位" )]
        public string Wechat { get; set; }
        /// <summary>
        /// 传真
        /// </summary>
        [DisplayName( "传真" )]
        [StringLength( 50, ErrorMessage = "传真输入过长，不能超过50位" )]
        public string Fax { get; set; }
        /// <summary>
        /// 省份标识
        /// </summary>
        [DisplayName( "省份标识" )]
        public Guid? ProvinceId { get; set; }
        /// <summary>
        /// 省份
        /// </summary>
        [DisplayName( "省份" )]
        [StringLength( 100, ErrorMessage = "省份输入过长，不能超过100位" )]
        public string Province { get; set; }
        /// <summary>
        /// 城市标志
        /// </summary>
        [DisplayName( "城市标志" )]
        public Guid? CityId { get; set; }
        /// <summary>
        /// 城市
        /// </summary>
        [DisplayName( "城市" )]
        [StringLength( 100, ErrorMessage = "城市输入过长，不能超过100位" )]
        public string City { get; set; }
        /// <summary>
        /// 区县标识
        /// </summary>
        [DisplayName( "区县标识" )]
        public Guid? CountyId { get; set; }
        /// <summary>
        /// 区县
        /// </summary>
        [DisplayName( "区县" )]
        [StringLength( 100, ErrorMessage = "区县输入过长，不能超过100位" )]
        public string County { get; set; }
        /// <summary>
        /// 街道
        /// </summary>
        [DisplayName( "街道" )]
        [StringLength( 200, ErrorMessage = "街道输入过长，不能超过200位" )]
        public string Street { get; set; }
        /// <summary>
        /// 邮政编码
        /// </summary>
        [DisplayName( "邮政编码" )]
        [StringLength( 20, ErrorMessage = "邮政编码输入过长，不能超过20位" )]
        public string Zip { get; set; }
        /// <summary>
        /// 身份证
        /// </summary>
        [DisplayName( "身份证" )]
        [StringLength( 50, ErrorMessage = "身份证输入过长，不能超过50位" )]
        public string IdCard { get; set; }
        /// <summary>
        /// 头像
        /// </summary>
        [DisplayName( "头像" )]
        [StringLength( 500, ErrorMessage = "头像输入过长，不能超过500位" )]
        public string Avatar { get; set; }
        /// <summary>
        /// 身份证正面
        /// </summary>
        [DisplayName( "身份证正面" )]
        [StringLength( 500, ErrorMessage = "身份证正面输入过长，不能超过500位" )]
        public string IdCardFront { get; set; }
        /// <summary>
        /// 身份证反面
        /// </summary>
        [DisplayName( "身份证反面" )]
        [StringLength( 500, ErrorMessage = "身份证反面输入过长，不能超过500位" )]
        public string IdCardReverse { get; set; }
        /// <summary>
        /// 文化程度
        /// </summary>
        [DisplayName( "文化程度" )]
        public int? Degree { get; set; }
        /// <summary>
        /// 毕业学校
        /// </summary>
        [DisplayName( "毕业学校" )]
        [StringLength( 50, ErrorMessage = "毕业学校输入过长，不能超过50位" )]
        public string SchoolOfGraduate { get; set; }
        /// <summary>
        /// 学习专业
        /// </summary>
        [DisplayName( "学习专业" )]
        [StringLength( 50, ErrorMessage = "学习专业输入过长，不能超过50位" )]
        public string Discipline { get; set; }
        /// <summary>
        /// 工作单位
        /// </summary>
        [DisplayName( "工作单位" )]
        [StringLength( 50, ErrorMessage = "工作单位输入过长，不能超过50位" )]
        public string Company { get; set; }
        /// <summary>
        /// 个人简历
        /// </summary>
        [DisplayName( "个人简历" )]
        public string Resume { get; set; }
        /// <summary>
        /// 拼音简码
        /// </summary>
        [DisplayName( "拼音简码" )]
        [Required(ErrorMessage = "拼音简码不能为空")]
        [StringLength( 200, ErrorMessage = "拼音简码输入过长，不能超过200位" )]
        public string PinYin { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [DisplayName( "备注" )]
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        public string Note { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [DisplayName( "创建时间" )]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [DisplayName( "创建人" )]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [DisplayName( "最后修改时间" )]
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [DisplayName( "最后修改人" )]
        public Guid? LastModifierId { get; set; }
        /// <summary>
        /// 租户标识
        /// </summary>
        [DisplayName( "租户标识" )]
        [StringLength( 50, ErrorMessage = "租户标识输入过长，不能超过50位" )]
        public string TenantId { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [DisplayName( "是否删除" )]
        public bool IsDeleted { get; set; }
        
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions() {
            AddDescription( t => t.Id );
            AddDescription( t => t.Code );
            AddDescription( t => t.Name );
            AddDescription( t => t.FirstName );
            AddDescription( t => t.LastName );
            AddDescription( t => t.NickName );
            AddDescription( t => t.EnglishName );
            AddDescription( t => t.Gender );
            AddDescription( t => t.Birthday );
            AddDescription( t => t.NativePlace );
            AddDescription( t => t.Nation );
            AddDescription( t => t.Phone );
            AddDescription( t => t.Email );
            AddDescription( t => t.Qq );
            AddDescription( t => t.Wechat );
            AddDescription( t => t.Fax );
            AddDescription( t => t.ProvinceId );
            AddDescription( t => t.Province );
            AddDescription( t => t.CityId );
            AddDescription( t => t.City );
            AddDescription( t => t.CountyId );
            AddDescription( t => t.County );
            AddDescription( t => t.Street );
            AddDescription( t => t.Zip );
            AddDescription( t => t.IdCard );
            AddDescription( t => t.Avatar );
            AddDescription( t => t.IdCardFront );
            AddDescription( t => t.IdCardReverse );
            AddDescription( t => t.Degree );
            AddDescription( t => t.SchoolOfGraduate );
            AddDescription( t => t.Discipline );
            AddDescription( t => t.Company );
            AddDescription( t => t.Resume );
            AddDescription( t => t.PinYin );
            AddDescription( t => t.Note );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.LastModificationTime );
            AddDescription( t => t.LastModifierId );
            AddDescription( t => t.TenantId );
        }
        
        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( UserInfo other ) {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.FirstName, other.FirstName );
            AddChange( t => t.LastName, other.LastName );
            AddChange( t => t.NickName, other.NickName );
            AddChange( t => t.EnglishName, other.EnglishName );
            AddChange( t => t.Gender, other.Gender );
            AddChange( t => t.Birthday, other.Birthday );
            AddChange( t => t.NativePlace, other.NativePlace );
            AddChange( t => t.Nation, other.Nation );
            AddChange( t => t.Phone, other.Phone );
            AddChange( t => t.Email, other.Email );
            AddChange( t => t.Qq, other.Qq );
            AddChange( t => t.Wechat, other.Wechat );
            AddChange( t => t.Fax, other.Fax );
            AddChange( t => t.ProvinceId, other.ProvinceId );
            AddChange( t => t.Province, other.Province );
            AddChange( t => t.CityId, other.CityId );
            AddChange( t => t.City, other.City );
            AddChange( t => t.CountyId, other.CountyId );
            AddChange( t => t.County, other.County );
            AddChange( t => t.Street, other.Street );
            AddChange( t => t.Zip, other.Zip );
            AddChange( t => t.IdCard, other.IdCard );
            AddChange( t => t.Avatar, other.Avatar );
            AddChange( t => t.IdCardFront, other.IdCardFront );
            AddChange( t => t.IdCardReverse, other.IdCardReverse );
            AddChange( t => t.Degree, other.Degree );
            AddChange( t => t.SchoolOfGraduate, other.SchoolOfGraduate );
            AddChange( t => t.Discipline, other.Discipline );
            AddChange( t => t.Company, other.Company );
            AddChange( t => t.Resume, other.Resume );
            AddChange( t => t.PinYin, other.PinYin );
            AddChange( t => t.Note, other.Note );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
            AddChange( t => t.TenantId, other.TenantId );
        }
    }
}