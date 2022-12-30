﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    /// <summary>
    /// 日志配置项
    /// </summary>
    public class LogOptions
    {
        /// <summary>
        /// 最低日志输出级别
        /// </summary>
        public LogLevel MinLevel { get; set; }

        /// <summary>
        /// 输出到控制台
        /// </summary>
        public EnableOption Console { get; set; } = new EnableOption();

        /// <summary>
        /// 输出到调试
        /// </summary>
        public EnableOption Debug { get; set; } = new EnableOption();

        /// <summary>
        /// 输出到文件
        /// </summary>
        public EnableOption File { get; set; } = new EnableOption();
        /// <summary>
        /// 输出到钉钉
        /// </summary>
        public EnableOption Dingding { set; get; } = new EnableOption();

        /// <summary>
        /// 输出到ES
        /// </summary>
        public ElasticsearchOption Elasticsearch { get; set; } = new ElasticsearchOption();

        /// <summary>
        /// 重写日志级别 
        /// </summary>
        public List<OverrideOption> Overrides { get; set; } = new List<OverrideOption>();

        /// <summary>
        /// 输出到exceptionless
        /// </summary>
        public EnableOption Exceptionless { get; set; } = new EnableOption();
    }
}
