/* Copyright (C) 2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;

namespace Utilities
{
    /// <remarks>
    /// https://docs.microsoft.com/en-us/windows/win32/debug/pe-format#the-rsrc-section
    /// </remarks>
    public class ResourceDirectoryTableHeader
    {
        public uint Characteristics;
        public uint TimeDateStamp;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ushort NumberOfNameEntries;
        public ushort NumberOfIDEntries;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Characteristics);
            writer.Write(TimeDateStamp);
            writer.Write(MajorVersion);
            writer.Write(MinorVersion);
            writer.Write(NumberOfNameEntries);
            writer.Write(NumberOfIDEntries);
        }

        public static ResourceDirectoryTableHeader Parse(BinaryReader reader)
        {
            ResourceDirectoryTableHeader header = new ResourceDirectoryTableHeader();
            header.Characteristics = reader.ReadUInt32();
            header.TimeDateStamp = reader.ReadUInt32();
            header.MajorVersion = reader.ReadUInt16();
            header.MinorVersion = reader.ReadUInt16();
            header.NumberOfNameEntries = reader.ReadUInt16();
            header.NumberOfIDEntries = reader.ReadUInt16();
            return header;
        }
    }
}
