using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utilities
{
    public class BinaryWriterUtils
    {
        public static void WriteFixedLengthAsciiString(BinaryWriter writer, string str, int fixedSize)
        {
            if (str.Length > fixedSize)
            {
                str = str.Substring(0, fixedSize);
            }
            byte[] buffer = Encoding.ASCII.GetBytes(str);

            writer.Write(buffer);
            int bytesWritten = buffer.Length;
            while (bytesWritten < fixedSize)
            {
                writer.Write((byte)0);
                bytesWritten++;
            }
        }

        public static void WriteNullTerminatedAsciiString(BinaryWriter writer, string str)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            writer.Write(buffer);
            writer.Write((byte)0);
        }
    }
}
