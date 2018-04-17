using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Bing.Utils.Extensions;

namespace Bing.Datas.Queries.Trees
{
    /// <summary>
    /// 树型查询参数
    /// </summary>
    /// <typeparam name="TParentId">父标识类型</typeparam>
    public class TreeQueryParameter<TParentId>:QueryParameter,ITreeQueryParameter<TParentId>
    {
        /// <summary>
        /// 父编号
        /// </summary>
        public TParentId ParentId { get; set; }

        /// <summary>
        /// 级数
        /// </summary>
        public int? Level { get; set; }

        /// <summary>
        /// 路径
        /// </summary>
        private string _path = string.Empty;

        /// <summary>
        /// 路径
        /// </summary>
        public string Path
        {
            get => _path == null ? string.Empty : _path.Trim();
            set => _path = value;
        }

        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool? Enabled { get; set; }

        /// <summary>
        /// 初始化一个<see cref="TreeQueryParameter{TParentId}"/>类型的实例
        /// </summary>
        protected TreeQueryParameter()
        {
            Order = "SortId";
        }

        /// <summary>
        /// 是否搜索
        /// </summary>
        /// <returns></returns>
        public bool IsSearch()
        {
            var items = Bing.Utils.Helpers.Reflection.GetPublicProperties(this);
            return items.All(t => Ignore(t.Text, t.Value)) == false;
        }

        /// <summary>
        /// 是否忽略
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        private bool Ignore(string name, object value)
        {
            if (value.SafeString().IsEmpty())
            {
                return true;
            }

            switch (name.SafeString().ToLower())
            {
                case "order":
                case "pagesize":
                case "page":
                case "totalcount":
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 树型查询参数
    /// </summary>
    public class TreeQueryParameter: TreeQueryParameter<Guid?>, ITreeQueryParameter { }
}
