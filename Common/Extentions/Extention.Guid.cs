using System;

namespace CEF.Common.Extentions
{
    public static partial class Extention
    {
        public static string GuidTo16String(this Guid guid)
        {
            string base64 = Convert.ToBase64String(guid.ToByteArray());

            string encoded = base64.Replace("/", "_").Replace("+", "-");

            return encoded.Substring(0, 22);
        }
    }
}
