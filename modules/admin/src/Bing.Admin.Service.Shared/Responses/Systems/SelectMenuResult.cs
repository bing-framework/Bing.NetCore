using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Admin.Systems.Domain.Models;
using Bing.Extensions;

namespace Bing.Admin.Service.Shared.Responses.Systems
{
    /// <summary>
    /// 选择菜单结果
    /// </summary>
    public class SelectMenuResult
    {
        /// <summary>
        /// 菜单数据
        /// </summary>
        private readonly IEnumerable<Module> _menuData;

        /// <summary>
        /// 按钮数据
        /// </summary>
        private readonly IEnumerable<Operation> _buttonData;

        /// <summary>
        /// 权限数据
        /// </summary>
        private readonly IEnumerable<Guid> _permissionData;

        /// <summary>
        /// 菜单结果
        /// </summary>
        private readonly List<SelectModuleResponse> _result;

        /// <summary>
        /// 初始化一个<see cref="SelectMenuResult"/>类型的实例
        /// </summary>
        /// <param name="menuData">菜单数据</param>
        /// <param name="buttonData">按钮数据</param>
        /// <param name="permissionData">权限数据</param>
        public SelectMenuResult(IEnumerable<Module> menuData
            , IEnumerable<Operation> buttonData
            , IEnumerable<Guid> permissionData)
        {
            _menuData = menuData;
            _buttonData = buttonData;
            _permissionData = permissionData;
            _result = new List<SelectModuleResponse>();
        }

        /// <summary>
        /// 获取树形结果
        /// </summary>
        public List<SelectModuleResponse> GetResult()
        {
            if (_menuData == null)
                return _result;
            foreach (var root in _menuData.Where(IsRoot))
                AddNodes(root);
            return _result.OrderBy(x => x.SortId).ToList();
        }

        /// <summary>
        /// 是否根节点
        /// </summary>
        /// <param name="dto">菜单响应</param>
        protected bool IsRoot(Module dto)
        {
            if (_menuData.Any(t => t.ParentId.IsEmpty()))
                return dto.ParentId.IsEmpty();
            return dto.Level == _menuData.Min(t => t.Level);
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="root">根节点</param>
        private void AddNodes(Module root)
        {
            var rootNode = ToNode(root);
            _result.Add(rootNode);
            AddChildren(rootNode);
        }

        /// <summary>
        /// 转换为树节点
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        private SelectModuleResponse ToNode(Module dto)
        {
            var result = new SelectModuleResponse
            {
                Id = dto.Id,
                ApplicationId = dto.ApplicationId.SafeValue(),
                ParentId = dto.ParentId,
                Name = dto.Name,
                Url = dto.Url,
                Icon = dto.Icon,
                SortId = dto.SortId
            };
            if (_permissionData.Any(x => x == dto.Id))
                result.Selected = true;
            return result;
        }

        /// <summary>
        /// 添加子节点
        /// </summary>
        /// <param name="node">节点</param>
        private void AddChildren(SelectModuleResponse node)
        {
            if (node == null)
                return;
            node.Children = GetChildren(node.Id).Select(ToNode).ToList();
            node.Buttons = GetModuleButtons(node.Id).Select(ToButton).ToList();
            foreach (var child in node.Children)
                AddChildren(child);
        }

        /// <summary>
        /// 转换为按钮
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        private SelectOperationResponse ToButton(Operation dto)
        {
            var result = new SelectOperationResponse
            {
                Id = dto.Id,
                ModuleId = dto.ModuleId,
                Name = dto.Name,
                Code = dto.Code,
                Icon = dto.Icon,
                SortId = dto.SortId
            };
            if (_permissionData.Any(x => x == dto.Id))
                result.Selected = true;
            return result;
        }

        /// <summary>
        /// 获取节点直接下级
        /// </summary>
        /// <param name="parentId">父标识</param>
        private List<Module> GetChildren(Guid? parentId) => _menuData.Where(t => t.ParentId == parentId).ToList();

        /// <summary>
        /// 获取模块按钮列表
        /// </summary>
        /// <param name="moduleId">模块标识</param>
        private List<Operation> GetModuleButtons(Guid moduleId) => _buttonData.Where(t => t.ModuleId == moduleId).ToList();
    }
}
