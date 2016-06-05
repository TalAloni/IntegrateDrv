using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;
using Utilities;

namespace IntegrateDrv
{
    public partial class INIFile
    {
        public void ReadPacked(string filePath)
        {
            byte[] bytes = FileSystemUtils.ReadFile(filePath);
            byte[] unpackedBytes = Unpack(bytes, this.FileName);
            m_encoding = GetEncoding(ref unpackedBytes);
            this.Text = m_encoding.GetString(unpackedBytes);
        }

        public void ReadPackedFromDirectory(string directoryPath)
        {
            if (m_fileName == String.Empty)
            {
                throw new Exception("ReadFileFromDirectory - class has not been initizalized with a file name");
            }
            ReadPacked(directoryPath + this.PackedFileName);
        }

        public void ReadPackedCriticalFileFromDirectory(string directoryPath)
        {
            try
            {
                ReadPackedFromDirectory(directoryPath);
            }
            catch (CabException)
            {
                Console.WriteLine("Error: Cannot unpack '{0}', Cab file is corrupted.", this.PackedFileName);
                Program.Exit();
            }
        }

        public void SavePacked(string path)
        {
            // if an existing file was read, this.Text will contain the BOM character, otherwise we write ASCII and there is no need for BOM.
            byte[] unpackedBytes = m_encoding.GetBytes(this.Text);
            byte[] bytes = Pack(unpackedBytes, this.FileName);
            FileSystemUtils.ClearReadOnlyAttribute(path);
            FileSystemUtils.WriteFile(path, bytes);
            this.IsModified = false;
        }

        public void SavePackedToDirectory(string directory)
        {
            SavePacked(directory + this.PackedFileName);
        }

        public static byte[] Pack(byte[] unpackedBytes, string unpackedFileName)
        {
            MemoryStream unpackedStream = new MemoryStream(unpackedBytes);
            BasicPackStreamContext streamContext = new BasicPackStreamContext(unpackedStream);

            List<string> fileNames = new List<string>();
            fileNames.Add(unpackedFileName);
            using (CabEngine engine = new CabEngine())
            {
                engine.Pack(streamContext, fileNames);
            }
            Stream packedStream = streamContext.ArchiveStream;
            if (packedStream != null)
            {
                packedStream.Position = 0;

                byte[] packedBytes = new byte[packedStream.Length];
                packedStream.Read(packedBytes, 0, packedBytes.Length);
                return packedBytes;
            }
            else
            {
                string message = String.Format("Error: File '{0}' failed to be repacked");
                Console.WriteLine(message);
                Program.Exit();
                throw new Exception(message);
            }
        }

        public static byte[] Unpack(byte[] fileBytes, string unpackedFileName)
        {
            MemoryStream packedStream = new MemoryStream(fileBytes);
            BasicUnpackStreamContext streamContext = new BasicUnpackStreamContext(packedStream);

            Predicate<string> isFileMatch = delegate(string match) { return String.Compare(match, unpackedFileName, true) == 0; };
            using (CabEngine engine = new CabEngine())
            {
                engine.Unpack(streamContext, isFileMatch);
            }
            Stream unpackedStream = streamContext.FileStream;
            if (unpackedStream != null)
            {
                unpackedStream.Position = 0;

                byte[] unpackedBytes = new byte[unpackedStream.Length];
                unpackedStream.Read(unpackedBytes, 0, unpackedBytes.Length);

                return unpackedBytes;
            }
            else
            {
                string message = String.Format("Error: File does not contain the expected file ('{1}')", unpackedFileName);
                Console.WriteLine(message);
                Program.Exit();
                return new byte[0];
            }
        }

        public string PackedFileName
        {
            get
            {
                return this.FileName.Substring(0, this.FileName.Length - 1) + "_";
            }
        }
    }
}
