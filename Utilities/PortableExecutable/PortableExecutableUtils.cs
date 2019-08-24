/* Copyright (C) 2011 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace Utilities
{
    public class PortableExecutableUtils
    {
        private static BinaryReader GetBinaryReader(string path)
        {
            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BinaryReader reader = new BinaryReader(stream);
            return reader;
        }

        public static List<string> GetDependencies(string path)
        {
            List<string> result = new List<string>();
            PortableExecutableInfo peInfo = new PortableExecutableInfo(path);

            ImportDirectory dir = peInfo.ImportDirectory;
            if (dir != null)
            {
                BinaryReader reader = GetBinaryReader(path);
                foreach (ImageImportDescriptor desc in dir.Descriptors)
                {
                    uint fileNameOffset = peInfo.GetOffsetFromRVA(desc.NameRVA);
                    reader.BaseStream.Seek(fileNameOffset, SeekOrigin.Begin);
                    string fileName = BinaryReaderUtils.ReadNullTerminatedAsciiString(reader);
                    result.Add(fileName);
                }
                reader.Close();
            }
            return result;
        }

        public static void RenameDependencyFileName(string filePath, string oldFileName, string newFileName)
        {
            if (oldFileName.Length != newFileName.Length)
            {
                throw new NotImplementedException("when renaming dependencies, old file name must have the same size as new file name");
            }

            PortableExecutableInfo peInfo = new PortableExecutableInfo(filePath);
            uint oldNameRVA = 0;
            PESectionHeader header = null;
            int sectionIndex = -1;

            foreach (ImageImportDescriptor descriptor in peInfo.ImportDirectory.Descriptors)
            {
                uint nameRVA = descriptor.NameRVA;
                header = peInfo.FindSectionByRVA(nameRVA);
                if (header != null)
                {
                    sectionIndex = peInfo.SectionHeaders.IndexOf(header);

                    uint fileNameAddressInSection = peInfo.GetAddressInSectionFromRVA(header, nameRVA);

                    string fileName = PortableExecutableUtils.ReadNullTerminatedAsciiString(peInfo.Sections[sectionIndex], fileNameAddressInSection);
                    if (fileName.Equals(oldFileName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        oldNameRVA = nameRVA;
                    }
                }
            }

            if (oldNameRVA > 0)
            {
                byte[] newFileNameAsciiBytes = ASCIIEncoding.ASCII.GetBytes(newFileName);
                uint addressInSection = peInfo.GetAddressInSectionFromRVA(header, oldNameRVA);
                byte[] section = peInfo.Sections[sectionIndex];
                Buffer.BlockCopy(newFileNameAsciiBytes, 0, section, (int)addressInSection, newFileNameAsciiBytes.Length);
            }
            PortableExecutableInfo.WritePortableExecutable(peInfo, filePath);
        }

        public static string ReadNullTerminatedAsciiString(byte[] bytes, uint startIndex)
        {
            int index = (int)startIndex;
            using (MemoryStream ms = new MemoryStream())
            {
                Byte lastByte = 0;

                do
                {
                    lastByte = bytes[index];
                    ms.WriteByte(lastByte);
                    index++;
                }
                while ((lastByte > 0) && (index < bytes.Length));

                return ASCIIEncoding.ASCII.GetString(ms.GetBuffer(), 0, (Int32)(ms.Length - 1));
            }
        }

        // Adapted from:
        // http://stackoverflow.com/questions/6429779/can-anyone-define-the-windows-pe-checksum-algorithm
        /// <param name="checksumOffset">offset of the checksum withing the file</param>
        public static uint CalculcateChecksum(byte[] fileBytes, uint checksumOffset)
        {
            long checksum = 0;
            double top = Math.Pow(2, 32);

            int remainder = fileBytes.Length % 4;
            byte[] paddedFileBytes;
            if (remainder > 0)
            {
                paddedFileBytes = new byte[fileBytes.Length + 4 - remainder];
                Buffer.BlockCopy(fileBytes, 0, paddedFileBytes, 0, fileBytes.Length);
            }
            else
            {
                // no need to pad
                paddedFileBytes = fileBytes;
            }

            for (int i = 0; i < paddedFileBytes.Length / 4; i++)
            {
                if (i == checksumOffset / 4)
                {
                    continue;
                }
                uint dword = BitConverter.ToUInt32(paddedFileBytes, i * 4);
                checksum = (checksum & 0xffffffff) + dword + (checksum >> 32);
                if (checksum > top)
                {
                    checksum = (checksum & 0xffffffff) + (checksum >> 32);
                }
            }

            checksum = (checksum & 0xffff) + (checksum >> 16);
            checksum = (checksum) + (checksum >> 16);
            checksum = checksum & 0xffff;

            // The length is the one from the original fileBytes, not the padded one
            checksum += (uint)fileBytes.Length;
            return (uint)checksum;
        }

    }
}
