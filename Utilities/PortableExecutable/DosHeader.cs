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
    public sealed class DosHeader
    {
        public const ushort DosSignature = 0x5a4d; // MZ

        public ushort BytesOnLastPage;
        public ushort PageCount;
        public ushort RelocationCount;
        public ushort HeaderSize;
        public ushort MinExtraParagraphs;
        public ushort MaxExtraParagraphs;
        public ushort InitialSS;
        public ushort InitialSP;
        public ushort Checksum;
        public ushort InitialIP;
        public ushort InitialCS;
        public ushort RelocationTableOffset;
        public ushort OverlayNumber;
        public ushort OemID;
        public ushort OemInfo;
        public uint CoffHeaderOffset;

        public static DosHeader Parse(BinaryReader reader)
        {
            ushort signature = reader.ReadUInt16();
            if (DosSignature != signature)
            {
                throw new Exception("Invalid Dos header signature");
            }

            DosHeader header = new DosHeader();

            header.BytesOnLastPage = reader.ReadUInt16();
            header.PageCount = reader.ReadUInt16();
            header.RelocationCount = reader.ReadUInt16();
            header.HeaderSize = reader.ReadUInt16();
            header.MinExtraParagraphs = reader.ReadUInt16();
            header.MaxExtraParagraphs = reader.ReadUInt16();
            header.InitialSS = reader.ReadUInt16();
            header.InitialSP = reader.ReadUInt16();
            header.Checksum = reader.ReadUInt16();
            header.InitialIP = reader.ReadUInt16();
            header.InitialCS = reader.ReadUInt16();
            header.RelocationTableOffset = reader.ReadUInt16();
            header.OverlayNumber = reader.ReadUInt16();

            // reserved words
            for (int i = 0; i < 4; i++) reader.ReadUInt16();

            header.OemID = reader.ReadUInt16();
            header.OemInfo = reader.ReadUInt16();

            // reserved words
            for (int i = 0; i < 10; i++) reader.ReadUInt16();

            header.CoffHeaderOffset = reader.ReadUInt32();

            return header;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(DosSignature);
            writer.Write(BytesOnLastPage);
            writer.Write(PageCount);
            writer.Write(RelocationCount);
            writer.Write(HeaderSize);
            writer.Write(MinExtraParagraphs);
            writer.Write(MaxExtraParagraphs);
            writer.Write(InitialSS);
            writer.Write(InitialSP);
            writer.Write(Checksum);
            writer.Write(InitialIP);
            writer.Write(InitialCS);
            writer.Write(RelocationTableOffset);
            writer.Write(OverlayNumber);

            // reserved words
            for (int i = 0; i < 4; i++) writer.Write((ushort)0);
            writer.Write(OemID);
            writer.Write(OemInfo);

            // reserved words
            for (int i = 0; i < 10; i++) writer.Write((ushort)0);
            writer.Write(CoffHeaderOffset);
        }
    }
}