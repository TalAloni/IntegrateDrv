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
    /// <summary>
    /// Coff - Common Object File Format
    /// </summary>
    public sealed class CoffHeader
    {
        const uint NTSignature = 0x4550; // PE00

        public CoffMachine Machine;
        public ushort NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public CoffFlags Characteristics;

        public static CoffHeader Parse(BinaryReader reader)
        {
            uint signature = reader.ReadUInt32();
            if (NTSignature != signature)
            {
                throw new Exception("Invalid COFF header signature");
            }
            CoffHeader header = new CoffHeader();
            header.Machine = (CoffMachine)reader.ReadUInt16();
            header.NumberOfSections = reader.ReadUInt16();
            header.TimeDateStamp = reader.ReadUInt32();
            header.PointerToSymbolTable = reader.ReadUInt32();
            header.NumberOfSymbols = reader.ReadUInt32();
            header.SizeOfOptionalHeader = reader.ReadUInt16();
            header.Characteristics = (CoffFlags)reader.ReadUInt16();

            return header;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(NTSignature);
            writer.Write((ushort)Machine);
            writer.Write(NumberOfSections);
            writer.Write(TimeDateStamp);
            writer.Write(PointerToSymbolTable);
            writer.Write(NumberOfSymbols);
            writer.Write(SizeOfOptionalHeader);
            writer.Write((ushort)Characteristics);
        }
    }
}
