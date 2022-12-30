using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    /// <summary>
    /// 树模型（可以作为父类）
    /// </summary>
    public class TreeModel
    {
        /// <summary>
        /// 唯一标识Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 数据值
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 父Id
        /// </summary>
        public string ParentId { get; set; }

        /// <summary>
        /// 节点深度
        /// </summary>
        public int? Level { get; set; } = 1;

        /// <summary>
        /// 显示的内容
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 孩子节点
        /// </summary>
        public List<object> Children { get; set; }
    }
}
