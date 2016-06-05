using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utilities
{
    public class PortableExecutableInfo
    {
        private DosHeader m_dosHeader;
        private byte[] m_dosStubBytes; // Dos program stub is here ("This program cannot be run in DOS mode.")
        private CoffHeader m_coffHeader;
        private PEHeader m_peHeader;
        private List<PESectionHeader> m_sectionHeaders = new List<PESectionHeader>();
        private byte[] m_filler;
        private List<byte[]> m_sections = new List<byte[]>();
        private byte[] m_remainingBytes; // Digital signature is here
        private uint m_peHeaderOffset;

        ImportDirectory m_importDirectory;
             
        public PortableExecutableInfo(string path)
        {
            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BinaryReader reader = new BinaryReader(stream);
            Parse(reader);
            reader.Close(); // closes the underlying stream as well
        }

        public PortableExecutableInfo(byte[] fileBytes)
        {
            MemoryStream stream = new MemoryStream(fileBytes);
            BinaryReader reader = new BinaryReader(stream);
            Parse(reader);
            reader.Close(); // closes the underlying stream as well
        }

        public void Parse(BinaryReader reader)
        {
            m_dosHeader = DosHeader.Parse(reader);
            int dosStubSize = (int)(m_dosHeader.CoffHeaderOffset - reader.BaseStream.Position);
            m_dosStubBytes = reader.ReadBytes(dosStubSize);
            m_coffHeader = CoffHeader.Parse(reader);
            m_peHeaderOffset = (uint)reader.BaseStream.Position;
            m_peHeader = PEHeader.Parse(reader);

            for (int i = 0; i < m_coffHeader.NumberOfSections; i++)
            {
                m_sectionHeaders.Add(PESectionHeader.Parse(reader));
            }

            int fillerSize = (int)(m_sectionHeaders[0].PointerToRawData - reader.BaseStream.Position);
            m_filler = reader.ReadBytes(fillerSize);
            
            for (int i = 0; i < m_coffHeader.NumberOfSections; i++)
            {
                byte[] sectionBytes = reader.ReadBytes((int)m_sectionHeaders[i].SizeOfRawData);
                m_sections.Add(sectionBytes);
            }

            int remainingByteCount = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            m_remainingBytes = reader.ReadBytes(remainingByteCount);
            // file ends here

            // Parse Import Directory:
            PEDataDirectory importDirectoryEntry = m_peHeader.DataDirectories[(int)DataDirectoryName.Import];
            if (importDirectoryEntry.VirtualAddress > 0)
            {
                uint importDirectoryFileOffset = GetOffsetFromRVA(importDirectoryEntry.VirtualAddress);
                reader.BaseStream.Seek(importDirectoryFileOffset, SeekOrigin.Begin);
                m_importDirectory = ImportDirectory.Parse(reader);
            }
        }

        public void WritePortableExecutable(BinaryWriter writer)
        {
            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            m_dosHeader.Write(writer);
            writer.Write(m_dosStubBytes);
            m_coffHeader.Write(writer);
            m_peHeaderOffset = (uint)writer.BaseStream.Position;
            m_peHeader.Write(writer);
            for (int i = 0; i < m_coffHeader.NumberOfSections; i++)
            {
                m_sectionHeaders[i].Write(writer);
            }

            writer.Write(m_filler);
            for (int i = 0; i < m_coffHeader.NumberOfSections; i++)
            {
                writer.Write(m_sections[i]);
            }

            writer.Write(m_remainingBytes);
            
            // Write Import Directory:
            PEDataDirectory importDirectoryEntry = m_peHeader.DataDirectories[(int)DataDirectoryName.Import];
            if (importDirectoryEntry.VirtualAddress > 0)
            {
                uint importDirectoryFileOffset = GetOffsetFromRVA(importDirectoryEntry.VirtualAddress);
                writer.Seek((int)importDirectoryFileOffset, SeekOrigin.Begin);
                m_importDirectory.Write(writer);
            }

            // Update PE checksum:
            writer.Seek(0, SeekOrigin.Begin);
            byte[] fileBytes = new byte[writer.BaseStream.Length];
            writer.BaseStream.Read(fileBytes, 0, (int)writer.BaseStream.Length);
            uint checksumOffset = m_peHeaderOffset + PEHeader.ChecksumRelativeAddress;
            uint checksum = PortableExecutableUtils.CalculcateChecksum(fileBytes, checksumOffset);
            writer.Seek((int)checksumOffset, SeekOrigin.Begin);
            writer.Write(checksum);
            writer.Flush();
        }

        public PESectionHeader FindSectionByRVA(uint rva)
        {
            for (int i = 0; i < m_sectionHeaders.Count; i++)
            {
                uint sectionStart = m_sectionHeaders[i].VirtualAdress;
                uint sectionEnd = sectionStart + m_sectionHeaders[i].VirtualSize;

                if (rva >= sectionStart && rva < sectionEnd)
                {
                    return m_sectionHeaders[i];
                }
            }

            return null;
        }

        public uint GetOffsetFromRVA(uint rva)
        {
            PESectionHeader sectionHeader = FindSectionByRVA(rva);
            if (sectionHeader == null)
            {
                throw new Exception("Invalid PE file");
            }

            uint index = (sectionHeader.PointerToRawData + (rva - sectionHeader.VirtualAdress));
            return index;
        }

        public uint GetRVAFromAddressInSection(PESectionHeader sectionHeader, uint addressInSection)
        {
            uint rva = addressInSection + sectionHeader.VirtualAdress;
            return rva;
        }

        public uint GetRVAFromOffset(PESectionHeader sectionHeader, uint offset)
        {
            uint rva = offset + sectionHeader.VirtualAdress - sectionHeader.PointerToRawData;
            return rva;
        }

        public uint GetAddressInSectionFromRVA(PESectionHeader sectionHeader, uint rva)
        {
            uint addressInSection = rva - sectionHeader.VirtualAdress;
            return addressInSection;
        }

        public DosHeader DosHeader
        {
            get
            {
                return m_dosHeader;
            }
        }

        public CoffHeader CoffHeader
        {
            get
            {
                return m_coffHeader;
            }
        }

        public PEHeader PEHeader
        {
            get
            {
                return m_peHeader;
            }
        }

        public List<byte[]> Sections
        {
            get
            {
                return m_sections;
            }
        }

        public List<PESectionHeader> SectionHeaders
        {
            get
            {
                return m_sectionHeaders;
            }
        }

        public uint PEHeaderOffset
        {
            get
            {
                return m_peHeaderOffset;
            }
        }

        public ImportDirectory ImportDirectory
        {
            get
            {
                return m_importDirectory;
            }
        }

        public static void WritePortableExecutable(PortableExecutableInfo peInfo, string path)
        {
            FileSystemUtils.ClearReadOnlyAttribute(path);
            FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
            WritePortableExecutable(peInfo, stream);
            stream.Close();
        }

        public static void WritePortableExecutable(PortableExecutableInfo peInfo, Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            peInfo.WritePortableExecutable(writer);
            writer.Close();
        }
    }
}
