using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    /// <summary>
    /// 文件信息
    /// </summary>
    public struct FileEntry
    {
        public FileEntry(string fileName, byte[] fileBytes)
        {
            FileName = fileName;
            FileBytes = fileBytes;
        }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件字节
        /// </summary>
        public byte[] FileBytes { get; set; }
    }
}
