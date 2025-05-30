﻿using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Biz.Enums;
using Bing.Data;
using Bing.Domain.Entities;

namespace Bing.Admin.Commons.Domain.Models;

/// <summary>
/// 用户信息
/// </summary>
[Display(Name = "用户信息")]
public partial class UserInfo : AggregateRoot<UserInfo>,ISoftDelete, IAuditedObjectWithName
{
    /// <summary>
    /// 初始化一个<see cref="UserInfo"/>类型的实例
    /// </summary>
    public UserInfo() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="UserInfo"/>类型的实例
    /// </summary>
    /// <param name="id">用户信息标识</param>
    public UserInfo(Guid id) : base(id) { }

    /// <summary>
    /// 编码
    ///</summary>
    [Display(Name = "编码")]
    [StringLength( 100, ErrorMessage = "编码输入过长，不能超过100位" )]
    public string Code { get; set; }
    
    /// <summary>
    /// 姓名
    ///</summary>
    [Display(Name = "姓名")]
    [StringLength( 256, ErrorMessage = "姓名输入过长，不能超过256位" )]
    public string Name { get; set; }
    
    /// <summary>
    /// 名
    ///</summary>
    [Display(Name = "名")]
    [StringLength( 200, ErrorMessage = "名输入过长，不能超过200位" )]
    public string FirstName { get; set; }
    
    /// <summary>
    /// 姓
    ///</summary>
    [Display(Name = "姓")]
    [StringLength( 200, ErrorMessage = "姓输入过长，不能超过200位" )]
    public string LastName { get; set; }
    
    /// <summary>
    /// 昵称
    ///</summary>
    [Display(Name = "昵称")]
    [StringLength( 50, ErrorMessage = "昵称输入过长，不能超过50位" )]
    public string Nickname { get; set; }
    
    /// <summary>
    /// 英文名
    ///</summary>
    [Display(Name = "英文名")]
    [StringLength( 200, ErrorMessage = "英文名输入过长，不能超过200位" )]
    public string EnglishName { get; set; }
    
    /// <summary>
    /// 性别
    ///</summary>
    [Display(Name = "性别")]
    public Gender? Gender { get; set; }
    
    /// <summary>
    /// 出生日期
    ///</summary>
    [Display(Name = "出生日期")]
    public DateTime? Birthday { get; set; }
    
    /// <summary>
    /// 籍贯
    ///</summary>
    [Display(Name = "籍贯")]
    [StringLength( 50, ErrorMessage = "籍贯输入过长，不能超过50位" )]
    public string NativePlace { get; set; }
    
    /// <summary>
    /// 民族
    ///</summary>
    [Display(Name = "民族")]
    public int? Nation { get; set; }
    
    /// <summary>
    /// 手机号
    ///</summary>
    [Display(Name = "手机号")]
    [StringLength( 50, ErrorMessage = "手机号输入过长，不能超过50位" )]
    public string Phone { get; set; }
    
    /// <summary>
    /// 电子邮件
    ///</summary>
    [Display(Name = "电子邮件")]
    [StringLength( 500, ErrorMessage = "电子邮件输入过长，不能超过500位" )]
    public string Email { get; set; }
    
    /// <summary>
    /// QQ
    ///</summary>
    [Display(Name = "QQ")]
    [StringLength( 20, ErrorMessage = "QQ输入过长，不能超过20位" )]
    public string Qq { get; set; }
    
    /// <summary>
    /// 微信
    ///</summary>
    [Display(Name = "微信")]
    [StringLength( 20, ErrorMessage = "微信输入过长，不能超过20位" )]
    public string Wechat { get; set; }
    
    /// <summary>
    /// 传真
    ///</summary>
    [Display(Name = "传真")]
    [StringLength( 50, ErrorMessage = "传真输入过长，不能超过50位" )]
    public string Fax { get; set; }
    
    /// <summary>
    /// 省份编号
    ///</summary>
    [Display(Name = "省份编号")]
    public Guid? ProvinceId { get; set; }
    
    /// <summary>
    /// 省份
    ///</summary>
    [Display(Name = "省份")]
    [StringLength( 100, ErrorMessage = "省份输入过长，不能超过100位" )]
    public string Province { get; set; }
    
    /// <summary>
    /// 城市编号
    ///</summary>
    [Display(Name = "城市编号")]
    public Guid? CityId { get; set; }
    
    /// <summary>
    /// 城市
    ///</summary>
    [Display(Name = "城市")]
    [StringLength( 100, ErrorMessage = "城市输入过长，不能超过100位" )]
    public string City { get; set; }
    
    /// <summary>
    /// 区县编号
    ///</summary>
    [Display(Name = "区县编号")]
    public Guid? DistrictId { get; set; }
    
    /// <summary>
    /// 区县
    ///</summary>
    [Display(Name = "区县")]
    [StringLength( 100, ErrorMessage = "区县输入过长，不能超过100位" )]
    public string District { get; set; }
    
    /// <summary>
    /// 街道地址
    ///</summary>
    [Display(Name = "街道地址")]
    [StringLength( 200, ErrorMessage = "街道地址输入过长，不能超过200位" )]
    public string Street { get; set; }
    
    /// <summary>
    /// 邮政编码
    ///</summary>
    [Display(Name = "邮政编码")]
    [StringLength( 20, ErrorMessage = "邮政编码输入过长，不能超过20位" )]
    public string Zip { get; set; }
    
    /// <summary>
    /// 身份证
    ///</summary>
    [Display(Name = "身份证")]
    [StringLength( 50, ErrorMessage = "身份证输入过长，不能超过50位" )]
    public string IdCard { get; set; }
    
    /// <summary>
    /// 备注
    ///</summary>
    [Display(Name = "备注")]
    [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
    public string Note { get; set; }
    
    /// <summary>
    /// 拼音简码
    ///</summary>
    [Display(Name = "拼音简码")]
    [Required(ErrorMessage = "拼音简码不能为空")]
    [StringLength( 50, ErrorMessage = "拼音简码输入过长，不能超过50位" )]
    public string PinYin { get; set; }
    
    /// <summary>
    /// 头像
    ///</summary>
    [Display(Name = "头像")]
    [StringLength( 500, ErrorMessage = "头像输入过长，不能超过500位" )]
    public string Avatar { get; set; }
    
    /// <summary>
    /// 身份证正面
    ///</summary>
    [Display(Name = "身份证正面")]
    [StringLength( 500, ErrorMessage = "身份证正面输入过长，不能超过500位" )]
    public string IdCardFront { get; set; }
    
    /// <summary>
    /// 身份证反面
    ///</summary>
    [Display(Name = "身份证反面")]
    [StringLength( 500, ErrorMessage = "身份证反面输入过长，不能超过500位" )]
    public string IdCardReverse { get; set; }
    
    /// <summary>
    /// 文化程度
    ///</summary>
    [Display(Name = "文化程度")]
    public int? Degree { get; set; }
    
    /// <summary>
    /// 毕业学校
    ///</summary>
    [Display(Name = "毕业学校")]
    [StringLength( 50, ErrorMessage = "毕业学校输入过长，不能超过50位" )]
    public string SchoolOfGraduate { get; set; }
    
    /// <summary>
    /// 学校专业
    ///</summary>
    [Display(Name = "学校专业")]
    [StringLength( 50, ErrorMessage = "学校专业输入过长，不能超过50位" )]
    public string Discipline { get; set; }
    
    /// <summary>
    /// 工作单位
    ///</summary>
    [Display(Name = "工作单位")]
    [StringLength( 50, ErrorMessage = "工作单位输入过长，不能超过50位" )]
    public string Company { get; set; }
    
    /// <summary>
    /// 个人简历
    ///</summary>
    [Display(Name = "个人简历")]
    public string Resume { get; set; }
    
    /// <summary>
    /// 是否删除
    ///</summary>
    [Display(Name = "是否删除")]
    public bool IsDeleted { get; set; }
    
    /// <summary>
    /// 创建时间
    ///</summary>
    [Display(Name = "创建时间")]
    public DateTime? CreationTime { get; set; }
    
    /// <summary>
    /// 创建人编号
    ///</summary>
    [Display(Name = "创建人编号")]
    public Guid? CreatorId { get; set; }
    
    /// <summary>
    /// 创建人
    ///</summary>
    [Display(Name = "创建人")]
    [StringLength( 200, ErrorMessage = "创建人输入过长，不能超过200位" )]
    public string Creator { get; set; }
    
    /// <summary>
    /// 最后修改时间
    ///</summary>
    [Display(Name = "最后修改时间")]
    public DateTime? LastModificationTime { get; set; }
    
    /// <summary>
    /// 最后修改人编号
    ///</summary>
    [Display(Name = "最后修改人编号")]
    public Guid? LastModifierId { get; set; }
    
    /// <summary>
    /// 最后修改人
    ///</summary>
    [Display(Name = "最后修改人")]
    [StringLength( 200, ErrorMessage = "最后修改人输入过长，不能超过200位" )]
    public string LastModifier { get; set; }
}
