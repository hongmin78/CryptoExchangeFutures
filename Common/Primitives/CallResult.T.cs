using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    /// <summary>
    /// Ajax请求结果
    /// </summary>
    public class CallResult<T> : CallResult
    {
        /// <summary>
        /// 返回数据
        /// </summary>
        public T? Data { get; set; }
    }
}