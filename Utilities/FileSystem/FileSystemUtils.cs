using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Utilities
{
	public class FileSystemUtils
	{
        public static bool IsDirectoryExist(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            return dir.Exists;
        }

        public static bool IsFileExist(string path)
        {
            return File.Exists(path);
        }

        public static void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// This Method does not support files with length over 4GB
        /// </summary>
		public static byte[] ReadFile(string path)
		{
			FileStream fileStream = new FileStream(path,FileMode.Open, FileAccess.Read);
			int fileLength = Convert.ToInt32(fileStream.Length);
			byte[] fileBytes = new byte[fileLength];

			fileStream.Read(fileBytes,0,fileLength);
            
			fileStream.Close();
			return fileBytes;
		}

        public static void ClearReadOnlyAttribute(string path)
        {
            FileInfo file = new FileInfo(path);
            if (file.Exists)
            {
                file.IsReadOnly = false;
            }
        }

        public static void WriteFile(string path, byte[] bytes)
        {
            WriteFile(path, bytes, FileMode.Create);
        }

        public static void WriteFile(string path, byte[] bytes, FileMode fileMode)
        {
            FileInfo file = new FileInfo(path);

            FileStream stream = file.Open(fileMode, FileAccess.Write);
            stream.Write(bytes, 0, bytes.Length);
            stream.Close();
        }

        /// <summary>
        /// Extracts file / directory name from path
        /// </summary>
        public static string GetNameFromPath(string path)
        {
            string[] parts = path.Split('\\');
            if (parts.Length > 0)
            {
                if (parts[parts.Length - 1] == String.Empty)
                {
                    return parts[parts.Length - 2];
                }
                else
                {
                    return parts[parts.Length - 1];
                }
            }
            else
            {
                return String.Empty;
            }
        }
	}
}
