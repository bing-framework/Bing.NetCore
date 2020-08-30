using System.Collections.Generic;
using System.Linq;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Extensions;

namespace Bing.Admin.Service.Shared.Dtos.NgAlain
{
    /// <summary>
    /// 菜单结果
    /// </summary>
    public class MenuResult
    {
        /// <summary>
        /// 菜单数据
        /// </summary>
        private readonly IEnumerable<MenuResponse> _menuData;

        /// <summary>
        /// 按钮数据
        /// </summary>
        private readonly IEnumerable<ButtonResponse> _buttonData;

        /// <summary>
        /// 菜单结果
        /// </summary>
        private readonly List<MenuInfo> _result;

        /// <summary>
        /// 初始化一个<see cref="MenuResult"/>类型的实例
        /// </summary>
        /// <param name="menuData">菜单数据</param>
        /// <param name="buttonData">按钮数据</param>
        public MenuResult(IEnumerable<MenuResponse> menuData, IEnumerable<ButtonResponse> buttonData = null)
        {
            _menuData = menuData;
            _buttonData = buttonData ?? new List<ButtonResponse>();
            _result = new List<MenuInfo>();
        }

        /// <summary>
        /// 获取树形结果
        /// </summary>
        public List<MenuInfo> GetResult()
        {
            if (_menuData == null)
                return _result;
            foreach (var root in _menuData.Where(IsRoot))
                AddNodes(root);
            return _result;
        }

        /// <summary>
        /// 是否根节点
        /// </summary>
        /// <param name="dto">菜单响应</param>
        protected bool IsRoot(MenuResponse dto)
        {
            if (_menuData.Any(t => t.ParentId.IsEmpty()))
                return dto.ParentId.IsEmpty();
            return dto.Level == _menuData.Min(t => t.Level);
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="root">根节点</param>
        private void AddNodes(MenuResponse root)
        {
            var rootNode = ToNode(root);
            _result.Add(rootNode);
            AddChildren(rootNode);
        }

        /// <summary>
        /// 转换为树节点
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        private MenuInfo ToNode(MenuResponse dto)
        {
            var result = new MenuInfo
            {
                Id = dto.Id,
                Text = dto.Name,
                Icon = dto.Icon,
            };
            if (dto.External)
                result.ExternalLink = dto.Url;
            else
                result.Link = dto.Url;
            return result;
        }

        /// <summary>
        /// 添加子节点
        /// </summary>
        /// <param name="node">节点</param>
        private void AddChildren(MenuInfo node)
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
        private ButtonInfo ToButton(ButtonResponse dto)
        {
            var result = new ButtonInfo
            {
                Id = dto.Id,
                Code = dto.Code,
                Text = dto.Name,
                Icon = dto.Icon,
                SortId = dto.SortId
            };
            return result;
        }

        /// <summary>
        /// 获取节点直接下级
        /// </summary>
        /// <param name="parentId">父标识</param>
        private List<MenuResponse> GetChildren(string parentId) => _menuData.Where(t => t.ParentId == parentId).ToList();

        /// <summary>
        /// 获取模块按钮列表
        /// </summary>
        /// <param name="moduleId">模块标识</param>
        private List<ButtonResponse> GetModuleButtons(string moduleId) => _buttonData.Where(t => t.ModuleId == moduleId).ToList();
    }
}
