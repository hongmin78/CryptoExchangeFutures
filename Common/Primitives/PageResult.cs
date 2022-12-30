using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    /// <summary>
    /// 分页返回结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PageResult<T> : CallResult<List<T>>
    {
        /// <summary>
        /// 总记录数
        /// </summary>
        public int Total { get; set; }
    }
}
