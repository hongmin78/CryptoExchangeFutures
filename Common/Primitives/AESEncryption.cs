using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    public class AESEncryption
    {
        //默认密钥向量   
        private static byte[] _key1 = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

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
                    strKey += "0";
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

        #region use key and iv 

        /// <summary>  
        /// AES加密算法  
        /// </summary>  
        /// <param name="input">明文字符串</param>  
        /// <param name="key">密钥（32位）</param>  
        /// <param name="iv">IV（16位）</param>  
        /// <returns>字符串</returns>  
        public static string EncryptByAES(string input, string key, string iv)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key.Substring(0, 32));
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                aesAlg.Key = keyBytes;
                aesAlg.IV = Encoding.UTF8.GetBytes(iv.Substring(0, 16));

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(input);
                        }
                        byte[] bytes = msEncrypt.ToArray();
                        return ByteArrayToHexString(bytes);
                    }
                }
            }
        }

        /// <summary>  
        /// AES解密  
        /// </summary>  
        /// <param name="input">密文字节数组</param>  
        /// <param name="key">密钥（32位）</param>  
        /// <returns>返回解密后的字符串</returns>  
        /// <param name="iv">IV（16位）</param>  
        public static string DecryptByAES(string input, string key, string iv)
        {
            byte[] inputBytes = HexStringToByteArray(input);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key.Substring(0, 32));
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                aesAlg.Key = keyBytes;
                aesAlg.IV = Encoding.UTF8.GetBytes(iv.Substring(0, 16));

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream(inputBytes))
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srEncrypt = new StreamReader(csEncrypt))
                        {
                            return srEncrypt.ReadToEnd();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 将指定的16进制字符串转换为byte数组
        /// </summary>
        /// <param name="s">16进制字符串(如：“7F 2C 4A”或“7F2C4A”都可以)</param>
        /// <returns>16进制字符串对应的byte数组</returns>
        public static byte[] HexStringToByteArray(string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;
        }

        /// <summary>
        /// 将一个byte数组转换成一个格式化的16进制字符串
        /// </summary>
        /// <param name="data">byte数组</param>
        /// <returns>格式化的16进制字符串</returns>
        public static string ByteArrayToHexString(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            foreach (byte b in data)
            {
                //16进制数字
                sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
                //16进制数字之间以空格隔开
                //sb.Append(Convert.ToString(b, 16).PadLeft(2, '0').PadRight(3, ' '));
            }
            return sb.ToString().ToUpper();
        }
        #endregion
    }
}
