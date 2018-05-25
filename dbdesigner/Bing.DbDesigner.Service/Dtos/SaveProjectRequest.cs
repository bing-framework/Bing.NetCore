using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Bing.Applications.Dtos;

namespace Bing.DbDesigner.Service.Dtos
{
    /// <summary>
    /// 保存项目参数
    /// </summary>
    public class SaveProjectRequest:RequestBase
    {
        /// <summary>
        /// 项目ID
        /// </summary>
        [DataMember]
        public Guid? Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// 英文名
        /// </summary>
        [DataMember]
        public string EnglishName { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [DataMember]
        public string Remark { get; set; }
    }
}
