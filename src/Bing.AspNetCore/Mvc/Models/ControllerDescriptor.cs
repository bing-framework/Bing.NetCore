using System;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Bing.AspNetCore.Mvc.Models
{
    /// <summary>
    /// 控制器描述
    /// </summary>
    public class ControllerDescriptor
    {
        /// <summary>
        /// 区域
        /// </summary>
        public string Area { get; protected set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; protected set; }

        /// <summary>
        /// 类型信息
        /// </summary>
        [JsonIgnore]
        public TypeInfo TypeInfo { get; }

        /// <summary>
        /// 初始化一个<see cref="ControllerDescriptor"/>类型的实例
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public ControllerDescriptor(TypeInfo typeInfo)
        {
            TypeInfo = typeInfo;
            Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            InitName();
            InitArea();
            InitDescription();
        }

        /// <summary>
        /// 初始化名称
        /// </summary>
        protected virtual void InitName()
        {
            Name = TypeInfo.Name.Replace("Controller", "");
        }

        /// <summary>
        /// 初始化区域
        /// </summary>
        protected virtual void InitArea()
        {
            var attribute = Attribute.GetCustomAttribute(TypeInfo, typeof(AreaAttribute));
            if (attribute is AreaAttribute areaAttribute)
            {
                Area = areaAttribute.RouteValue;
            }
        }

        /// <summary>
        /// 初始化描述
        /// </summary>
        protected virtual void InitDescription()
        {
            var attribute = Attribute.GetCustomAttribute(TypeInfo, typeof(DescriptionAttribute));
            if (attribute is DescriptionAttribute descriptionAttribute)
            {
                Description = descriptionAttribute.Description;
            }
        }
    }
}
