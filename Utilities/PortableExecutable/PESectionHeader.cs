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
    public sealed class PESectionHeader
    {
        public string Name;
        public uint VirtualSize;
        public uint VirtualAdress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLineNumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLineNumbers;
        public SectionFlags Flags;

        public static PESectionHeader Parse(BinaryReader reader)
        {
            PESectionHeader header = new PESectionHeader();
            header.Name = BinaryReaderUtils.ReadFixedLengthAsciiString(reader, 8);
            header.VirtualSize = reader.ReadUInt32();
            header.VirtualAdress = reader.ReadUInt32();
            header.SizeOfRawData = reader.ReadUInt32();
            header.PointerToRawData = reader.ReadUInt32();
            header.PointerToRelocations = reader.ReadUInt32();
            header.PointerToLineNumbers = reader.ReadUInt32();
            header.NumberOfRelocations = reader.ReadUInt16();
            header.NumberOfLineNumbers = reader.ReadUInt16();
            header.Flags = (SectionFlags)reader.ReadUInt32();
            return header;
        }

        public void Write(BinaryWriter writer)
        { 
            BinaryWriterUtils.WriteFixedLengthAsciiString(writer, Name, 8);
            writer.Write(VirtualSize);
            writer.Write(VirtualAdress);
            writer.Write(SizeOfRawData);
            writer.Write(PointerToRawData);
            writer.Write(PointerToRelocations);
            writer.Write(PointerToLineNumbers);
            writer.Write(NumberOfRelocations);
            writer.Write(NumberOfLineNumbers);
            writer.Write((uint)Flags);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
