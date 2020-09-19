using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Domain.Entities;

namespace Bing.Data.Test.Integration.Samples.Bugs
{
    public class Issue1 : AggregateRoot<Issue1>, ISoftDelete, IAuditedObject
    {
        /// <summary>
        /// 初始化一个<see cref="Issue1"/>类型的实例
        /// </summary>
        public Issue1() : this(Guid.Empty)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="Issue1"/>类型的实例
        /// </summary>
        /// <param name="id">店铺标识</param>
        public Issue1(Guid id) : base(id)
        {
        }

        /// <summary>
        /// 商户标识
        /// </summary>
        [Display(Name = "商户标识")]
        public Guid? MerchantId { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        [Display(Name = "编码")]
        [Required(ErrorMessage = "编码不能为空")]
        [StringLength(50, ErrorMessage = "编码输入过长，不能超过50位")]
        public string Code { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [Display(Name = "名称")]
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength(200, ErrorMessage = "名称输入过长，不能超过200位")]
        public string Name { get; set; }

        /// <summary>
        /// 仓库标识
        /// </summary>
        [Display(Name = "仓库标识")]
        [Required(ErrorMessage = "仓库标识不能为空")]
        public Guid WarehouseId { get; set; }

        /// <summary>
        /// 仓库名称
        /// </summary>
        [Display(Name = "仓库名称")]
        [StringLength(200, ErrorMessage = "仓库名称输入过长，不能超过200位")]
        public string WarehouseName { get; set; }

        /// <summary>
        /// 类型
        /// </summary>
        [Display(Name = "类型")]
        [Required(ErrorMessage = "类型不能为空")]
        public int Type { get; set; }

        /// <summary>
        /// 网店编码
        /// </summary>
        [Display(Name = "网店编码")]
        [StringLength(50, ErrorMessage = "网店编码输入过长，不能超过50位")]
        public string EShopCode { get; set; }

        /// <summary>
        /// 联系人
        /// </summary>
        [Display(Name = "联系人")]
        [StringLength(50, ErrorMessage = "联系人输入过长，不能超过50位")]
        public string LinkmanName { get; set; }

        /// <summary>
        /// 联系人电话
        /// </summary>
        [Display(Name = "联系人电话")]
        [StringLength(30, ErrorMessage = "联系人电话输入过长，不能超过30位")]
        public string LinkmanPhone { get; set; }

        /// <summary>
        /// 国家标识
        /// </summary>
        [Display(Name = "国家标识")]
        public Guid? CountryId { get; set; }

        /// <summary>
        /// 国家
        /// </summary>
        [Display(Name = "国家")]
        [StringLength(100, ErrorMessage = "国家输入过长，不能超过100位")]
        public string Country { get; set; }

        /// <summary>
        /// 省份标识
        /// </summary>
        [Display(Name = "省份标识")]
        public Guid? ProvinceId { get; set; }

        /// <summary>
        /// 省份
        /// </summary>
        [Display(Name = "省份")]
        [StringLength(100, ErrorMessage = "省份输入过长，不能超过100位")]
        public string Province { get; set; }

        /// <summary>
        /// 城市标识
        /// </summary>
        [Display(Name = "城市标识")]
        public Guid? CityId { get; set; }

        /// <summary>
        /// 城市
        /// </summary>
        [Display(Name = "城市")]
        [StringLength(100, ErrorMessage = "城市输入过长，不能超过100位")]
        public string City { get; set; }

        /// <summary>
        /// 区县标识
        /// </summary>
        [Display(Name = "区县标识")]
        public Guid? CountyId { get; set; }

        /// <summary>
        /// 区县
        /// </summary>
        [Display(Name = "区县")]
        [StringLength(100, ErrorMessage = "区县输入过长，不能超过100位")]
        public string County { get; set; }

        /// <summary>
        /// 街道地址
        /// </summary>
        [Display(Name = "街道地址")]
        [StringLength(200, ErrorMessage = "街道地址输入过长，不能超过200位")]
        public string Street { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool Enabled { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name = "备注")]
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        public string Remark { get; set; }

        /// <summary>
        /// 是否删除
        /// </summary>
        [Display(Name = "是否删除")]
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Display(Name = "创建时间")]
        public DateTime? CreationTime { get; set; }

        /// <summary>
        /// 创建人标识
        /// </summary>
        [Display(Name = "创建人标识")]
        public Guid? CreatorId { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name = "创建人")]
        [StringLength(200, ErrorMessage = "创建人输入过长，不能超过200位")]
        public string Creator { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Display(Name = "最后修改时间")]
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        [Display(Name = "最后修改人标识")]
        public Guid? LastModifierId { get; set; }

        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display(Name = "最后修改人")]
        [StringLength(200, ErrorMessage = "最后修改人输入过长，不能超过200位")]
        public string LastModifier { get; set; }
        /// <summary>
        /// 扩展
        /// </summary>
        [Display(Name = "扩展")]
        public string Extend { get; set; }
    }
}
