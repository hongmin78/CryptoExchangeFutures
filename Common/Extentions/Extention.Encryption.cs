using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Extentions
{
    public static partial class Extention
    {
        private static readonly string _secretKey = "4zkENWqpfvJ63NWooLrtcXVd886uArMB";
        public static string CreateSign(this string content)
        {
            var hmacSha = new HMACSHA384(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmacSha.ComputeHash(Encoding.UTF8.GetBytes(content)).ToArray();
            return Convert.ToBase64String(hash);
        }
    }
}
