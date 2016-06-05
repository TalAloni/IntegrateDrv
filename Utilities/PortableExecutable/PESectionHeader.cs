//---------------------------------------------------------------------
// Authors: jachymko
//
// Description: Class which describes a DOS header.
//
// Creation Date: Dec 24, 2006
//---------------------------------------------------------------------
// Adapted by Tal Aloni, 2011.09.09

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

    [Flags]
    public enum SectionFlags : uint
    {
        TlsIndexScaled = 0x00000001,
        Code = 0x00000020,
        InitializedData = 0x00000040,
        UninitializedData = 0x00000080,
        DeferSpecExc = 0x00004000,
        NRelocOvfl = 0x01000000,
        Discardable = 0x02000000,
        NotCached = 0x04000000,
        NotPaged = 0x08000000,
        Shared = 0x10000000,
        Execute = 0x20000000,
        Read = 0x40000000,
        Write = 0x80000000,
    }
}
