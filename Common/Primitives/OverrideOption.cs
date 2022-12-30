using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    /// <summary>
    /// 重写
    /// </summary>
    public class OverrideOption
    {
        /// <summary>
        /// 源
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 最低级别
        /// </summary>
        public LogLevel MinLevel { get; set; }
    }
}
