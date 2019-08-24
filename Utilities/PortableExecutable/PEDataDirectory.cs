/* Copyright (C) 2011 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * Based on work by jachymko, Dec 24, 2006.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;

namespace Utilities
{
    public sealed class PEDataDirectory
    {
        public uint VirtualAddress;
        public uint Size;

        public static PEDataDirectory Parse(BinaryReader reader)
        {
            PEDataDirectory dir = new PEDataDirectory();
            dir.VirtualAddress = reader.ReadUInt32();
            dir.Size = reader.ReadUInt32();
            return dir;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(VirtualAddress);
            writer.Write(Size);
        }

        public override string ToString()
        {
            return string.Format("PEDataDirectory, RVA=0x{0:x}, Size=0x{1:x}", VirtualAddress, Size);
        }
    }
}
