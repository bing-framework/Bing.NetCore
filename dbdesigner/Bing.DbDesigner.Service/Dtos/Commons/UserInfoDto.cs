using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;

namespace Bing.DbDesigner.Service.Dtos.Commons {
    /// <summary>
    /// 用户信息数据传输对象
    /// </summary>
    public class UserInfoDto : DtoBase {
        /// <summary>
        /// 编码
        /// </summary>
        [StringLength( 100, ErrorMessage = "编码输入过长，不能超过100位" )]
        [Display( Name = "编码" )]
        [DataMember]
        public string Code { get; set; }
        /// <summary>
        /// 姓名
        /// </summary>
        [StringLength( 200, ErrorMessage = "姓名输入过长，不能超过200位" )]
        [Display( Name = "姓名" )]
        [DataMember]
        public string Name { get; set; }
        /// <summary>
        /// 名
        /// </summary>
        [StringLength( 200, ErrorMessage = "名输入过长，不能超过200位" )]
        [Display( Name = "名" )]
        [DataMember]
        public string FirstName { get; set; }
        /// <summary>
        /// 姓
        /// </summary>
        [StringLength( 200, ErrorMessage = "姓输入过长，不能超过200位" )]
        [Display( Name = "姓" )]
        [DataMember]
        public string LastName { get; set; }
        /// <summary>
        /// 昵称
        /// </summary>
        [StringLength( 50, ErrorMessage = "昵称输入过长，不能超过50位" )]
        [Display( Name = "昵称" )]
        [DataMember]
        public string NickName { get; set; }
        /// <summary>
        /// 英文名
        /// </summary>
        [StringLength( 200, ErrorMessage = "英文名输入过长，不能超过200位" )]
        [Display( Name = "英文名" )]
        [DataMember]
        public string EnglishName { get; set; }
        /// <summary>
        /// 性别
        /// </summary>
        [Display( Name = "性别" )]
        [DataMember]
        public int? Gender { get; set; }
        /// <summary>
        /// 出生日期
        /// </summary>
        [Display( Name = "出生日期" )]
        [DataMember]
        public DateTime? Birthday { get; set; }
        /// <summary>
        /// 籍贯
        /// </summary>
        [StringLength( 50, ErrorMessage = "籍贯输入过长，不能超过50位" )]
        [Display( Name = "籍贯" )]
        [DataMember]
        public string NativePlace { get; set; }
        /// <summary>
        /// 民族
        /// </summary>
        [Display( Name = "民族" )]
        [DataMember]
        public int? Nation { get; set; }
        /// <summary>
        /// 手机号
        /// </summary>
        [StringLength( 50, ErrorMessage = "手机号输入过长，不能超过50位" )]
        [Display( Name = "手机号" )]
        [DataMember]
        public string Phone { get; set; }
        /// <summary>
        /// 电子邮件
        /// </summary>
        [StringLength( 500, ErrorMessage = "电子邮件输入过长，不能超过500位" )]
        [Display( Name = "电子邮件" )]
        [DataMember]
        public string Email { get; set; }
        /// <summary>
        /// QQ
        /// </summary>
        [StringLength( 20, ErrorMessage = "QQ输入过长，不能超过20位" )]
        [Display( Name = "QQ" )]
        [DataMember]
        public string Qq { get; set; }
        /// <summary>
        /// 微信
        /// </summary>
        [StringLength( 50, ErrorMessage = "微信输入过长，不能超过50位" )]
        [Display( Name = "微信" )]
        [DataMember]
        public string Wechat { get; set; }
        /// <summary>
        /// 传真
        /// </summary>
        [StringLength( 50, ErrorMessage = "传真输入过长，不能超过50位" )]
        [Display( Name = "传真" )]
        [DataMember]
        public string Fax { get; set; }
        /// <summary>
        /// 省份标识
        /// </summary>
        [Display( Name = "省份标识" )]
        [DataMember]
        public Guid? ProvinceId { get; set; }
        /// <summary>
        /// 省份
        /// </summary>
        [StringLength( 100, ErrorMessage = "省份输入过长，不能超过100位" )]
        [Display( Name = "省份" )]
        [DataMember]
        public string Province { get; set; }
        /// <summary>
        /// 城市标志
        /// </summary>
        [Display( Name = "城市标志" )]
        [DataMember]
        public Guid? CityId { get; set; }
        /// <summary>
        /// 城市
        /// </summary>
        [StringLength( 100, ErrorMessage = "城市输入过长，不能超过100位" )]
        [Display( Name = "城市" )]
        [DataMember]
        public string City { get; set; }
        /// <summary>
        /// 区县标识
        /// </summary>
        [Display( Name = "区县标识" )]
        [DataMember]
        public Guid? CountyId { get; set; }
        /// <summary>
        /// 区县
        /// </summary>
        [StringLength( 100, ErrorMessage = "区县输入过长，不能超过100位" )]
        [Display( Name = "区县" )]
        [DataMember]
        public string County { get; set; }
        /// <summary>
        /// 街道
        /// </summary>
        [StringLength( 200, ErrorMessage = "街道输入过长，不能超过200位" )]
        [Display( Name = "街道" )]
        [DataMember]
        public string Street { get; set; }
        /// <summary>
        /// 邮政编码
        /// </summary>
        [StringLength( 20, ErrorMessage = "邮政编码输入过长，不能超过20位" )]
        [Display( Name = "邮政编码" )]
        [DataMember]
        public string Zip { get; set; }
        /// <summary>
        /// 身份证
        /// </summary>
        [StringLength( 50, ErrorMessage = "身份证输入过长，不能超过50位" )]
        [Display( Name = "身份证" )]
        [DataMember]
        public string IdCard { get; set; }
        /// <summary>
        /// 头像
        /// </summary>
        [StringLength( 500, ErrorMessage = "头像输入过长，不能超过500位" )]
        [Display( Name = "头像" )]
        [DataMember]
        public string Avatar { get; set; }
        /// <summary>
        /// 身份证正面
        /// </summary>
        [StringLength( 500, ErrorMessage = "身份证正面输入过长，不能超过500位" )]
        [Display( Name = "身份证正面" )]
        [DataMember]
        public string IdCardFront { get; set; }
        /// <summary>
        /// 身份证反面
        /// </summary>
        [StringLength( 500, ErrorMessage = "身份证反面输入过长，不能超过500位" )]
        [Display( Name = "身份证反面" )]
        [DataMember]
        public string IdCardReverse { get; set; }
        /// <summary>
        /// 文化程度
        /// </summary>
        [Display( Name = "文化程度" )]
        [DataMember]
        public int? Degree { get; set; }
        /// <summary>
        /// 毕业学校
        /// </summary>
        [StringLength( 50, ErrorMessage = "毕业学校输入过长，不能超过50位" )]
        [Display( Name = "毕业学校" )]
        [DataMember]
        public string SchoolOfGraduate { get; set; }
        /// <summary>
        /// 学习专业
        /// </summary>
        [StringLength( 50, ErrorMessage = "学习专业输入过长，不能超过50位" )]
        [Display( Name = "学习专业" )]
        [DataMember]
        public string Discipline { get; set; }
        /// <summary>
        /// 工作单位
        /// </summary>
        [StringLength( 50, ErrorMessage = "工作单位输入过长，不能超过50位" )]
        [Display( Name = "工作单位" )]
        [DataMember]
        public string Company { get; set; }
        /// <summary>
        /// 个人简历
        /// </summary>
        [Display( Name = "个人简历" )]
        [DataMember]
        public string Resume { get; set; }
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Required(ErrorMessage = "拼音简码不能为空")]
        [StringLength( 200, ErrorMessage = "拼音简码输入过长，不能超过200位" )]
        [Display( Name = "拼音简码" )]
        [DataMember]
        public string PinYin { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        [Display( Name = "备注" )]
        [DataMember]
        public string Note { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [Display( Name = "创建时间" )]
        [DataMember]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [Display( Name = "创建人" )]
        [DataMember]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Display( Name = "最后修改时间" )]
        [DataMember]
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display( Name = "最后修改人" )]
        [DataMember]
        public Guid? LastModifierId { get; set; }
        /// <summary>
        /// 租户标识
        /// </summary>
        [StringLength( 50, ErrorMessage = "租户标识输入过长，不能超过50位" )]
        [Display( Name = "租户标识" )]
        [DataMember]
        public string TenantId { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [Display( Name = "是否删除" )]
        [DataMember]
        public bool? IsDeleted { get; set; }
        /// <summary>
        /// 版本号
        /// </summary>
        [Display( Name = "版本号" )]
        [DataMember]
        public Byte[] Version { get; set; }
    }
}