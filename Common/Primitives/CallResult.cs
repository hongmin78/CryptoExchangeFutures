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
    public class CallResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// 错误代码
        /// </summary>
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 返回消息
        /// </summary>
        public string? Msg { get; set; }
    }
}
