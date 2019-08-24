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
    public class ResourceDirectoryEntry
    {
        public uint NameOffsetOrIntegerID;
        public bool IsDirectory; // True means DataOffset points to another resource directory table
        public uint DataOffset;  // 31 bits, points to either a ResourceDataEntry or ResourceDirectoryTableHeader

        public void Write(BinaryWriter writer)
        {
            writer.Write(NameOffsetOrIntegerID);
            uint isLeafAndOffset = Convert.ToUInt32(IsDirectory) << 31 | (DataOffset & 0x7FFFFFFF);
            writer.Write(isLeafAndOffset);
        }

        public static ResourceDirectoryEntry Parse(BinaryReader reader)
        {
            ResourceDirectoryEntry entry = new ResourceDirectoryEntry();
            entry.NameOffsetOrIntegerID = reader.ReadUInt32();
            uint isLeafAndOffset = reader.ReadUInt32();
            entry.IsDirectory = Convert.ToBoolean(isLeafAndOffset >> 31);
            entry.DataOffset = (isLeafAndOffset & 0x7FFFFFFF);
            return entry;
        }
    }
}
