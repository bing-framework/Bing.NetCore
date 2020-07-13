using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bing.Domains.ChangeTracking
{
    /// <summary>
    /// 变更值集合
    /// </summary>
    public class ChangedValueDescriptorCollection : IEnumerable<ChangedValueDescriptor>
    {
        /// <summary>
        /// 变更值列表
        /// </summary>
        private readonly IList<ChangedValueDescriptor> _list;

        /// <summary>
        /// 变更值名称列表
        /// </summary>
        private readonly IList<string> _changedNameList;

        /// <summary>
        /// 初始化一个<see cref="ChangedValueDescriptorCollection"/>类型的实例
        /// </summary>
        public ChangedValueDescriptorCollection()
        {
            _list = new List<ChangedValueDescriptor>();
            _changedNameList = new List<string>();
        }

        /// <summary>
        /// 初始化一个<see cref="ChangedValueDescriptorCollection"/>类型的实例
        /// </summary>
        /// <param name="descriptors">变更值描述符集合</param>
        public ChangedValueDescriptorCollection(ChangedValueDescriptorCollection descriptors) : this()
        {
            if (descriptors == null)
                throw new ArgumentNullException(nameof(descriptors));
            Populate(descriptors);
        }

        /// <summary>
        /// 获取迭代器
        /// </summary>
        public IEnumerator<ChangedValueDescriptor> GetEnumerator() => _list.GetEnumerator();

        /// <summary>
        /// 获取迭代器
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="descriptor">变更值描述符</param>
        public void Add(ChangedValueDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Description))
                return;
            // TODO: 此处会导致无法显示导航属性变更信息
            //if (_changedNameList.Contains(descriptor.PropertyName))
            //    return;
            _list.Add(descriptor);
            _changedNameList.Add(descriptor.PropertyName);
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="description">描述</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public void Add(string propertyName, string description, string oldValue, string newValue)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return;
            // TODO: 此处会导致无法显示导航属性变更信息
            //if (_changedNameList.Contains(propertyName))
            //    return;
            _list.Add(new ChangedValueDescriptor(propertyName, description, oldValue, newValue));
            _changedNameList.Add(propertyName);
        }

        /// <summary>
        /// 添加集合
        /// </summary>
        /// <param name="descriptors">变更值描述符集合</param>
        public void AddRange(IEnumerable<ChangedValueDescriptor> descriptors)
        {
            if (descriptors == null)
                return;
            foreach (var descriptor in descriptors)
                Add(descriptor);
        }

        /// <summary>
        /// 填充
        /// </summary>
        /// <param name="descriptors">变更值描述符集合</param>
        public void Populate(IEnumerable<ChangedValueDescriptor> descriptors)
        {
            if (descriptors == null || !descriptors.Any())
                return;
            var filtedDiscriptors = descriptors.Where(x => !_changedNameList.Contains(x.PropertyName)).ToList();
            if (!filtedDiscriptors.Any())
                return;
            foreach (var item in filtedDiscriptors)
                Add(item);
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void FlushCache()
        {
            _list.Clear();
            _changedNameList.Clear();
        }

        /// <summary>
        /// 输出变更信息
        /// </summary>
        public override string ToString()
        {
            if (!_list.Any())
                return string.Empty;
            var result = new StringBuilder();
            foreach (var item in _list)
                result.AppendLine(item.ToString());
            return result.ToString();
        }
    }
}
