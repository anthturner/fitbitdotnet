using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FitBit.NET
{
    public static class Utility
    {
        #region Debug Byte Operations
        internal static string StringValueOfByte(byte[] data, char delimiter)
        {
            if (data == null || data.Length == 0) { return ""; }
            var sb = new StringBuilder();
            foreach (var t in data)
            {
                if (sb.Length > 0) { sb.Append(delimiter); }
                sb.Append(String.Format("{0:X2}", t));
            }
            return sb.ToString();
        }

        internal static string StringValueOfByte(byte data)
        {
            return String.Format("{0:X2}", data);
        }
        #endregion

        internal static byte[] RandomChannelID
        {
            get
            {
                var chanID = new byte[2];
                Random r = new Random((int)DateTime.Now.Ticks);
                chanID[0] = (byte)r.Next(0, 254);
                chanID[1] = (byte)r.Next(0, 254);
                return chanID;
            }
        }

        internal static byte XOR(byte[] array)
        {
            byte XORTotal = 0;
            foreach (byte b in array)
            {
                XORTotal ^= b;
            }
            return XORTotal;
        }

        internal static byte[] Trim(byte[] sourceArray, int startIndex, int count)
        {
            byte[] result = new byte[count];
            for (int i = startIndex; i < (startIndex + count); i++)
                result[i - startIndex] = sourceArray[i];
            return result;
        }

        internal static byte[] Trim(byte[] sourceArray, int startIndex)
        {
            return Trim(sourceArray, startIndex, (sourceArray.Length - startIndex));
        }
    }
}
