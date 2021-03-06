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

namespace Utilities
{
    public sealed class PEHeader
    {
        public const int ChecksumRelativeAddress = 64;
        public PEHeaderType Type;

        public Version LinkerVersion;
        public Version OperatingSystemVersion;
        public Version ImageVersion;
        public Version SubsystemVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public uint BaseOfData;

        public ulong ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint Checksum;
        public PESubsystem Subsystem;
        public PEDllCharacteristics DllCharacteristics;
        public ulong SizeOfStackReserve;
        public ulong SizeOfStackCommit;
        public ulong SizeOfHeapReserve;
        public ulong SizeOfHeapCommit;
        public uint LoaderFlags;

        public PEDataDirectory[] DataDirectories;

        public PEHeader()
        {
        }

        public PEDataDirectory GetDataDirectory(DataDirectoryName index)
        {
            return DataDirectories[(int)(index)];
        }

        public static PEHeader Parse(BinaryReader reader)
        {
            PEHeaderType signature = (PEHeaderType)reader.ReadUInt16();
            if (!Enum.IsDefined(typeof(PEHeaderType), signature))
            {
                throw new Exception("Invalid PE header signature");
            }

            PEHeader header = new PEHeader();

            header.Type = signature;
            header.LinkerVersion = new Version(reader.ReadByte(), reader.ReadByte());
            header.SizeOfCode = reader.ReadUInt32();
            header.SizeOfInitializedData = reader.ReadUInt32();
            header.SizeOfUninitializedData = reader.ReadUInt32();
            header.AddressOfEntryPoint = reader.ReadUInt32();
            header.BaseOfCode = reader.ReadUInt32();

            if (signature == PEHeaderType.PE64)
            {
                header.ImageBase = reader.ReadUInt64();
            }
            else
            {
                header.BaseOfData = reader.ReadUInt32();
                header.ImageBase = reader.ReadUInt32();
            }

            header.SectionAlignment = reader.ReadUInt32();
            header.FileAlignment = reader.ReadUInt32();
            header.OperatingSystemVersion = new Version(reader.ReadUInt16(), reader.ReadUInt16());
            header.ImageVersion = new Version(reader.ReadUInt16(), reader.ReadUInt16());
            header.SubsystemVersion = new Version(reader.ReadUInt16(), reader.ReadUInt16());
            header.Win32VersionValue = reader.ReadUInt32();
            header.SizeOfImage = reader.ReadUInt32();
            header.SizeOfHeaders = reader.ReadUInt32();
            header.Checksum = reader.ReadUInt32();
            header.Subsystem = (PESubsystem)reader.ReadUInt16();
            header.DllCharacteristics = (PEDllCharacteristics)reader.ReadUInt16();

            if (signature == PEHeaderType.PE64)
            {
                header.SizeOfStackReserve = reader.ReadUInt64();
                header.SizeOfStackCommit = reader.ReadUInt64();
                header.SizeOfHeapReserve = reader.ReadUInt64();
                header.SizeOfHeapCommit = reader.ReadUInt64();
            }
            else
            {
                header.SizeOfStackReserve = reader.ReadUInt32();
                header.SizeOfStackCommit = reader.ReadUInt32();
                header.SizeOfHeapReserve = reader.ReadUInt32();
                header.SizeOfHeapCommit = reader.ReadUInt32();
            }

            header.LoaderFlags = reader.ReadUInt32();

            header.DataDirectories = new PEDataDirectory[reader.ReadUInt32()];
            for (int i = 0; i < header.DataDirectories.Length; i++)
            {
                header.DataDirectories[i] = PEDataDirectory.Parse(reader);
            }

            return header;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((ushort)Type);
            writer.Write((byte)LinkerVersion.Major);
            writer.Write((byte)LinkerVersion.Minor);
            writer.Write(SizeOfCode);
            writer.Write(SizeOfInitializedData);
            writer.Write(SizeOfUninitializedData);
            writer.Write(AddressOfEntryPoint);
            writer.Write(BaseOfCode);

            if (Type == PEHeaderType.PE64)
            {
                writer.Write(ImageBase);
            }
            else
            {
                writer.Write(BaseOfData);
                writer.Write((uint)ImageBase);
            }

            writer.Write(SectionAlignment);
            writer.Write(FileAlignment);
            writer.Write((ushort)OperatingSystemVersion.Major);
            writer.Write((ushort)OperatingSystemVersion.Minor);
            writer.Write((ushort)ImageVersion.Major);
            writer.Write((ushort)ImageVersion.Minor);
            writer.Write((ushort)SubsystemVersion.Major);
            writer.Write((ushort)SubsystemVersion.Minor);
            writer.Write(Win32VersionValue);
            writer.Write(SizeOfImage);
            writer.Write(SizeOfHeaders);
            writer.Write(Checksum);
            writer.Write((ushort)Subsystem);
            writer.Write((ushort)DllCharacteristics);

            if (Type == PEHeaderType.PE64)
            {
                writer.Write(SizeOfStackReserve);
                writer.Write(SizeOfStackCommit);
                writer.Write(SizeOfHeapReserve);
                writer.Write(SizeOfHeapCommit);
            }
            else
            {
                writer.Write((uint)SizeOfStackReserve);
                writer.Write((uint)SizeOfStackCommit);
                writer.Write((uint)SizeOfHeapReserve);
                writer.Write((uint)SizeOfHeapCommit);
            }
            writer.Write(LoaderFlags);

            writer.Write(DataDirectories.Length);
            for (int i = 0; i < DataDirectories.Length; i++)
            {
                DataDirectories[i].Write(writer);
            }
        }
    }
}
