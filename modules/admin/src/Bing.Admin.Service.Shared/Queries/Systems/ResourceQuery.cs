using System;
using System.ComponentModel.DataAnnotations;
using Bing.Datas.Queries.Trees;

namespace Bing.Admin.Service.Shared.Queries.Systems
{
    /// <summary>
    /// 资源查询参数
    /// </summary>
    public class ResourceQuery : TreeQueryParameter
    {
        /// <summary>
        /// 资源标识
        /// </summary>
        [Display(Name = "资源标识")]
        public Guid? ResourceId { get; set; }
        /// <summary>
        /// 应用程序标识
        /// </summary>
        [Display(Name = "应用程序标识")]
        public Guid? ApplicationId { get; set; }

        private string _uri = string.Empty;
        /// <summary>
        /// 资源识别号
        /// </summary>
        [Display(Name = "资源识别号")]
        public string Uri
        {
            get => _uri == null ? string.Empty : _uri.Trim();
            set => _uri = value;
        }

        private string _name = string.Empty;
        /// <summary>
        /// 资源名称
        /// </summary>
        [Display(Name = "资源名称")]
        public string Name
        {
            get => _name == null ? string.Empty : _name.Trim();
            set => _name = value;
        }
        /// <summary>
        /// 资源类型
        /// </summary>
        [Display(Name = "资源类型")]
        public int? Type { get; set; }

        private string _remark = string.Empty;
        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name = "备注")]
        public string Remark
        {
            get => _remark == null ? string.Empty : _remark.Trim();
            set => _remark = value;
        }

        private string _pinYin = string.Empty;
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Display(Name = "拼音简码")]
        public string PinYin
        {
            get => _pinYin == null ? string.Empty : _pinYin.Trim();
            set => _pinYin = value;
        }
        /// <summary>
        /// 起始创建时间
        /// </summary>
        [Display(Name = "起始创建时间")]
        public DateTime? BeginCreationTime { get; set; }
        /// <summary>
        /// 结束创建时间
        /// </summary>
        [Display(Name = "结束创建时间")]
        public DateTime? EndCreationTime { get; set; }
        /// <summary>
        /// 创建人标识
        /// </summary>
        [Display(Name = "创建人标识")]
        public Guid? CreatorId { get; set; }

        private string _creator = string.Empty;
        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name = "创建人")]
        public string Creator
        {
            get => _creator == null ? string.Empty : _creator.Trim();
            set => _creator = value;
        }
        /// <summary>
        /// 起始最后修改时间
        /// </summary>
        [Display(Name = "起始最后修改时间")]
        public DateTime? BeginLastModificationTime { get; set; }
        /// <summary>
        /// 结束最后修改时间
        /// </summary>
        [Display(Name = "结束最后修改时间")]
        public DateTime? EndLastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人标识
        /// </summary>
        [Display(Name = "最后修改人标识")]
        public Guid? LastModifierId { get; set; }

        private string _lastModifier = string.Empty;
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display(Name = "最后修改人")]
        public string LastModifier
        {
            get => _lastModifier == null ? string.Empty : _lastModifier.Trim();
            set => _lastModifier = value;
        }

        private string _extend = string.Empty;
        /// <summary>
        /// 扩展
        /// </summary>
        [Display(Name = "扩展")]
        public string Extend
        {
            get => _extend == null ? string.Empty : _extend.Trim();
            set => _extend = value;
        }
    }
}
