using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Extentions
{
    /// <summary>
    /// CRC拓展
    /// </summary>
    public static partial class CRC
    {
        /// <summary>
        /// 转换Crc16
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToCrc16Str(this string str)
        {
            var bytes = Encoding.Default.GetBytes(str);
            int crc = new Crc16().ComputeChecksum(bytes);
            return crc.ToString();
        }
    }

    public class Crc16
    {
        const int polynomial = 0xA001;
        int[] table = new int[256];

        public int ComputeChecksum(byte[] bytes)
        {
            int crc = 0;
            for (int i = 0; i < bytes.Length; ++i)
            {
                byte index = (byte)(crc ^ bytes[i]);
                crc = ((crc >> 8) ^ table[index]);
            }
            return crc;
        }

        public byte[] ComputeChecksumBytes(byte[] bytes)
        {
            int crc = ComputeChecksum(bytes);
            return BitConverter.GetBytes(crc);
        }

        public Crc16()
        {
            int value;
            int temp;
            for (int i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = ((value >> 1) ^ polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                table[i] = value;
            }
        }
    }
}
