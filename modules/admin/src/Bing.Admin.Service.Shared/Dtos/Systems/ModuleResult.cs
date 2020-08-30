using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Extensions;

namespace Bing.Admin.Service.Shared.Dtos.Systems
{
    /// <summary>
    /// 模块结果
    /// </summary>
    public class ModuleResult
    {
        /// <summary>
        /// 模块数据
        /// </summary>
        private readonly IEnumerable<Module> _data;

        /// <summary>
        /// 模块结果
        /// </summary>
        private readonly List<ModuleResponse> _result;

        /// <summary>
        /// 初始化一个<see cref="ModuleResult"/>类型的实例
        /// </summary>
        /// <param name="data">模块数据</param>
        public ModuleResult(IEnumerable<Module> data)
        {
            _data = data;
            _result = new List<ModuleResponse>();
        }

        /// <summary>
        /// 获取树形结果
        /// </summary>
        public List<ModuleResponse> GetResult()
        {
            if (_data == null)
                return _result;
            foreach (var root in _data.Where(IsRoot))
                AddNodes(root);
            return _result;
        }

        /// <summary>
        /// 是否根节点
        /// </summary>
        /// <param name="entity">实体</param>
        protected bool IsRoot(Module entity)
        {
            if (_data.Any(x => x.ParentId.IsEmpty()))
                return entity.ParentId.IsEmpty();
            return entity.Level == _data.Min(x => x.Level);
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="entity">实体</param>
        private void AddNodes(Module entity)
        {
            var rootNode = ToNode(entity);
            _result.Add(rootNode);
            AddChildren(rootNode);
        }

        /// <summary>
        /// 转换为树节点
        /// </summary>
        /// <param name="entity">实体</param>
        private ModuleResponse ToNode(Module entity)
        {
            var result = new ModuleResponse()
            {
                Id = entity.Id,
                ApplicationId = entity.ApplicationId.SafeValue(),
                ParentId = entity.ParentId,
                Name = entity.Name,
                Url = entity.Url,
                Icon = entity.Icon,
                Remark = entity.Remark,
                Enabled = entity.Enabled,
                SortId = entity.SortId
            };
            return result;
        }

        /// <summary>
        /// 添加子节点内
        /// </summary>
        /// <param name="node">节点</param>
        private void AddChildren(ModuleResponse node)
        {
            if (node == null)
                return;
            node.Children = GetChildren(node.Id).Select(ToNode).ToList();
            foreach (var child in node.Children)
                AddChildren(child);
        }

        /// <summary>
        /// 获取节点直接下级
        /// </summary>
        /// <param name="parentId">父标识</param>
        private List<Module> GetChildren(Guid? parentId) => _data.Where(x => x.ParentId == parentId).ToList();
    }
}
