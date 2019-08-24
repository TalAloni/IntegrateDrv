/* Copyright (C) 2011 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * Based on work by jachymko, Dec 24, 2006.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utilities
{
    public class BinaryReaderUtils
    {
        public static string ReadFixedLengthAsciiString(BinaryReader reader, int fixedSize)
        {
            byte[] buffer = reader.ReadBytes(fixedSize);
            int len = 0;

            for (len = 0; len < fixedSize; len++)
            {
                if (buffer[len] == 0) break;
            }

            if (len > 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, len);
            }

            return string.Empty;
        }

        public static string ReadNullTerminatedAsciiString(BinaryReader reader)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Byte lastByte = 0;

                do
                {
                    lastByte = reader.ReadByte();
                    ms.WriteByte(lastByte);
                }
                while ((lastByte > 0) && (reader.BaseStream.Position < reader.BaseStream.Length));

                return ASCIIEncoding.ASCII.GetString(ms.GetBuffer(), 0, (Int32)(ms.Length - 1));
            }
        }
    }
}
