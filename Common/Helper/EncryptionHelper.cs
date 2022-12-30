using CEF.Common.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CEF.Common.Helper
{
    public static class EncryptionHelper
    {
        /// <summary>
        /// 解密数据信息
        /// </summary>
        /// <param name="data">加密之后的数据</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static string Decode(string data, string key)
        {
            if (string.IsNullOrEmpty(data)) return string.Empty;
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
            if (string.IsNullOrEmpty(data)) return string.Empty;
            var securityKey = GetSecurityKey(key);
            var encodeData = AESEncryption.AESEncrypt(data, securityKey);
            return Convert.ToBase64String(encodeData);
        }
        /// <summary>
        /// 获取安全密钥
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static string GetSecurityKey(string key)
        {
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



        //默认密钥向量   
        private static byte[] _key1 = { 0x69, 0x33, 0x45, 0x79, 0xaa, 0xcd, 0x9D, 0xE0, 0x11, 0x78, 0x3A, 0x9B, 0xC9, 0xAa, 0x5D, 0x99 };

        private const string _privateKey = "I$$#rtutVH*&^ccfd!~cs_)__&&^^%%$VVB<<>>cswsdIDCM(((901sdIIIIDvaalld$$JDKFHAddsvvLLL09*&&sdaopaSSS";
        public static string Encode(string data)
        {
            var key = GetKey();
            var buffer = AESEncrypt(data, key);
            return Convert.ToBase64String(buffer);
        }
        public static string Decode(string data)
        {
            var key = GetKey();
            var buffer = Convert.FromBase64String(data);
            var plainData = AESDecrypt(buffer, key);
            return Encoding.UTF8.GetString(plainData);
        }
        private static string GetKey()
        {
            var md5 = MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(_privateKey));
            var sb = new StringBuilder();
            int index = 0;
            foreach (var item in md5)
            {
                if (index >= _privateKey.Length)
                {
                    index = 0;
                }
                byte key1 = (byte)(item << 2);
                byte key2 = (byte)(_privateKey[index] << 3);
                if (key1 < key2)
                {
                    sb.Append(key2);
                    sb.Append(_privateKey[index]);
                }
                else
                {
                    sb.Append(item);
                    sb.Append(key1);
                }
                index++;
            }
            var keyData = sb.ToString();
            if (keyData.Length >= 32)
            {
                return keyData.Substring(0, 32);
            }
            index = 0;
            sb.Clear();
            sb.Append(keyData);
            while (true)
            {
                foreach (var item in keyData)
                {
                    byte key = (byte)(item >> 2);
                    sb.Append(key);
                }
                if (sb.Length >= 32)
                {
                    break;
                }
            }
            return sb.ToString().Substring(0, 32);
        }

        /// <summary>  
        /// AES加密算法  
        /// </summary>  
        /// <param name="plainText">明文字符串</param>  
        /// <param name="strKey">密钥</param>  
        /// <returns>返回加密后的密文字节数组</returns>  
        public static byte[] AESEncrypt(string plainText, string strKey)
        {
            //分组加密算法  
            SymmetricAlgorithm des = Rijndael.Create();
            byte[] inputByteArray = Encoding.UTF8.GetBytes(plainText);//得到需要加密的字节数组      
                                                                      //设置密钥及密钥向量  
            des.Key = Encoding.UTF8.GetBytes(strKey);
            des.IV = _key1;
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            cs.FlushFinalBlock();
            byte[] cipherBytes = ms.ToArray();//得到加密后的字节数组  
            cs.Close();
            ms.Close();
            return cipherBytes;
        }

        /// <summary>
        /// 获取合格的秘钥
        /// </summary>
        /// <param name="strKey">秘钥</param>
        /// <returns></returns>
        public static string GetAvailableStr(string strKey)
        {
            if (strKey.Length > 32)
            {
                strKey = strKey.Substring(0, 32);
                return strKey;
            }
            else if (strKey.Length < 32)
            {
                var i = 32 - strKey.Length;
                while (i > 0)
                {
                    strKey += "5";
                    i--;
                }
                return strKey;
            }
            else
            {
                return strKey;
            }

        }

        /// <summary>  
        /// AES解密  
        /// </summary>  
        /// <param name="cipherText">密文字节数组</param>  
        /// <param name="strKey">密钥</param>  
        /// <returns>返回解密后的字符串</returns>  
        public static byte[] AESDecrypt(byte[] cipherText, string strKey)
        {
            SymmetricAlgorithm des = Rijndael.Create();
            des.Key = Encoding.UTF8.GetBytes(strKey);
            des.IV = _key1;
            byte[] decryptBytes = new byte[cipherText.Length];
            MemoryStream ms = new MemoryStream(cipherText);
            CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Read);
            int count = cs.Read(decryptBytes, 0, decryptBytes.Length);
            cs.Close();
            ms.Close();
            byte[] resultBytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                resultBytes[i] = decryptBytes[i];
            }
            return resultBytes;
        }

        /// <summary>
        /// 获取足够长度的key
        /// </summary>
        /// <param name="strKey"></param>
        /// <returns></returns>
        public static string GetEnoughLenKey(string strKey)
        {
            var bytes = new byte[32];
            if (!string.IsNullOrEmpty(strKey))
            {
                bytes = Encoding.UTF8.GetBytes(strKey);
                if (bytes.Length < 32)
                {
                    var newByte = new byte[32];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        newByte[i] = bytes[i];
                    }

                    bytes = newByte;
                }
                else if (bytes.Length > 32)
                {
                    var newByte = new byte[32];
                    for (int i = 0; i < 32; i++)
                    {
                        newByte[i] = bytes[i];
                    }

                    bytes = newByte;
                }
            }
            return Encoding.UTF8.GetString(bytes);
        }


        /// <summary>
        /// 判断是不是base64字符串
        /// </summary>
        /// <param name="base64Str"></param>
        /// <returns></returns>
        public static bool IsBase64(string base64Str)
        {
            try
            {
                Convert.FromBase64String(base64Str);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static string CreateSign(string secretKey, string content)
        {
            var hmacSha = new HMACSHA384(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmacSha.ComputeHash(Encoding.UTF8.GetBytes(content)).ToArray();
            return Convert.ToBase64String(hash);
        }
    }
}
