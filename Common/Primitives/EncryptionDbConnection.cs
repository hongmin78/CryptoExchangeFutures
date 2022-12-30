using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    public class EncryptionDbConnection
    {
        const string _ivVal = "h27EF7jQh30z9rsQL0aguzMsOxeJBbQuc1fAsMiczetoNHs4rePAqLSIgEP7Tze5dwn8cRoZHXHg5nF649hnmg3q9xkNWajZZnbgjkSAQPSt3BCTZJ3JiRJXtWZFwA2wEIhIboNKhHwwnAmxVrOd7zG7DeNnld3fNlOljTHAVisBkdzSzrJ5LPo51a5XAqp4DgI1L1d1KehP75MpFVNTtYuYWJk9NKZvSnX3ux6yeIlxxzXVnFhFCwBFMK42H3dZ";
        const string _securyKey = "@_i$d#op*c^&m#$%ea_ui_kld&~~@#asdd/*-8&6%%asdexchange*all$#@@assdvv-_1%$sdfdas8-+.0aadv__!!";

        private static byte[] FormatByte(string strVal, Encoding encoding)
        {
            return encoding.GetBytes(Base64(strVal).Substring(0, 16).ToUpper());
        }
        public static string GenerateKey()
        {
            var guid = Guid.NewGuid().ToString().Replace("-", "");
            var key = GetSecurityKey(_securyKey, false);
            return Encode(guid, key);
        }

        public static string DecodeSecurityKey(string data)
        {
            var key = GetSecurityKey(_securyKey, false);
            var decodeData = Decode(data, key);
            return decodeData;
        }
        /// <summary>
        /// 解密数据信息
        /// </summary>
        /// <param name="data">加密之后的数据</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static string Decode(string data, string key)
        {
            byte[] buffer = Convert.FromBase64String(data);
            var securityKey = GetSecurityKey(key);
            var decodeData = AESEncryption.AESDecrypt(buffer, securityKey);
            return Encoding.UTF8.GetString(decodeData);
        }
        /// <summary>
        /// 加密数据
        /// </summary>
        /// <param name="data">原始数据信息</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static string Encode(string data, string key)
        {
            var securityKey = GetSecurityKey(key);
            var encodeData = AESEncryption.AESEncrypt(data, securityKey);
            return Convert.ToBase64String(encodeData);
        }
        /// <summary>
        /// 获取安全密钥
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static string GetSecurityKey(string key, bool isMD5 = true)
        {
            if (isMD5)
            {
                var md5 = MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(key));
                key = Convert.ToBase64String(md5);
            }
            if (key.Length >= 32)
            {
                return key.Substring(0, 32);
            }
            StringBuilder sb = new StringBuilder();
            int length = 0;
            int step = key.Length % 5;
            while (true)
            {

                for (int i = 0; i < key.Length; i += step)
                {
                    sb.Append(key[i]);
                    length++;
                    if (length >= 32)
                    {
                        return sb.ToString();
                    }
                }
            }
        }
        public static string Base64(string value)
        {
            var btArray = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(btArray, 0, btArray.Length);
        }
    }
}
