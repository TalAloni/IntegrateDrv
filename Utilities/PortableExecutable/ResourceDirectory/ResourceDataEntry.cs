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
    public class ResourceDataEntry
    {
        public uint DataRVA;
        public uint Size;
        public uint CodePage;
        public uint Reserved;

        public void Write(BinaryWriter writer)
        {
            writer.Write(DataRVA);
            writer.Write(Size);
            writer.Write(CodePage);
            writer.Write(Reserved);
        }

        public static ResourceDataEntry Parse(BinaryReader reader)
        {
            ResourceDataEntry entry = new ResourceDataEntry();
            entry.DataRVA = reader.ReadUInt32();
            entry.Size = reader.ReadUInt32();
            entry.CodePage = reader.ReadUInt32();
            entry.Reserved = reader.ReadUInt32();
            return entry;
        }
    }
}
