using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class ExportedRegistry : INIFile
    {
        public ExportedRegistry(string filePath) : base()
        {
            this.Text = ReadUnicode(filePath);
        }

        public ExportedRegistryKey LocalMachine
        {
            get
            {
                return new ExportedRegistryKey(this, "HKEY_LOCAL_MACHINE");
            }
        }

        public object GetValue(string keyName, string valueName)
        {
            string lineStart = String.Format("\"{0}\"=", valueName);
            string lineFound;
            int lineIndex = GetLineStartIndex(keyName, lineStart, out lineFound);
            if (lineIndex >= 0)
            {
                string valueStr = lineFound.Substring(lineStart.Length);
                object result = ParseValueDataString(valueStr);
                return result;
            }
            else
            {
                return null;
            }
        }

        protected int GetLineStartIndex(string sectionName, string lineStart, out string lineFound)
        {
            Predicate<string> lineStartsWith = delegate(string line) { return line.TrimStart(' ').StartsWith(lineStart, StringComparison.InvariantCultureIgnoreCase); };
            return GetLineIndex(sectionName, lineStartsWith, out lineFound, true);
        }

        public static object ParseValueDataString(string valueData)
        {
            RegistryValueKind valueKind;
            return ParseValueDataString(valueData, out valueKind);
        }

        public static object ParseValueDataString(string valueData, out RegistryValueKind valueKind)
        {
            if (valueData.StartsWith("dword:"))
            {
                valueKind = RegistryValueKind.DWord;
                valueData = valueData.Substring(6);
                try
                {
                    return Convert.ToInt32(valueData);
                }
                catch
                {
                    return null;
                }
            }
            else if (valueData.StartsWith("hex:"))
            {
                valueKind = RegistryValueKind.Binary;
                valueData = valueData.Substring(4);
                return ParseByteValueDataString(valueData);
            }
            else if (valueData.StartsWith("hex(7):"))
            {
                valueKind = RegistryValueKind.MultiString;
                valueData = valueData.Substring(7);
                byte[] bytes = ParseByteValueDataString(valueData);
                string str = Encoding.Unicode.GetString(bytes);
                return str.Split('\0');
            }
            else if (valueData.StartsWith("hex(2):"))
            {
                valueKind = RegistryValueKind.ExpandString;
                valueData = valueData.Substring(7);
                byte[] bytes = ParseByteValueDataString(valueData);
                return Encoding.Unicode.GetString(bytes);
            }
            else if (valueData.StartsWith("hex(b):"))
            {
                // little endian
                valueKind = RegistryValueKind.QWord;
                valueData = valueData.Substring(7);
                byte[] bytes = ParseByteValueDataString(valueData);
                return BitConverter.ToUInt64(bytes, 0);
            }
            else if (valueData.StartsWith("hex(4):"))
            {
                // little endian
                valueKind = RegistryValueKind.DWord;
                valueData = valueData.Substring(7);
                byte[] bytes = ParseByteValueDataString(valueData);
                return BitConverter.ToUInt32(bytes, 0);
            }
            else if (valueData.StartsWith("hex(5):"))
            {
                // big endian
                valueKind = RegistryValueKind.DWord;
                valueData = valueData.Substring(7);
                byte[] bytes = ParseByteValueDataString(valueData);
                byte[] reversedBytes = new byte[4];
                for (int index = 0; index < 4; index++)
                {
                    reversedBytes[index] = bytes[3 - index];
                }

                return BitConverter.ToUInt32(reversedBytes, 0);
            }
            else if (valueData.StartsWith("hex(0):"))
            {
                valueKind = RegistryValueKind.Unknown;
                valueData = valueData.Substring(7);
                return new byte[0];
            }
            else
            {
                valueKind = RegistryValueKind.String;
                return Unquote(valueData);
            }
        }

        public static byte[] ParseByteValueDataString(string valueData)
        {
            List<string> byteStringList = INIFile.GetCommaSeparatedValues(valueData);
            List<byte> byteList = new List<byte>();
            for (int index = 0; index < byteStringList.Count; index++)
            {
                byte data = Convert.ToByte(byteStringList[index], 16); // byte values are in Hex
                byteList.Add(data);
            }
            return byteList.ToArray();
        }

        public static string ReadUnicode(string filePath)
        {
            byte[] bytes = new byte[0];
            try
            {
                bytes = FileSystemUtils.ReadFile(filePath);
            }
            catch (IOException)
            {
                // usually it means the device is not ready (disconnected network drive / CD-ROM)
                Console.WriteLine("Error: IOException, Could not read file: " + filePath);
                Program.Exit();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Error: Access Denied, Could not read file: " + filePath);
                Program.Exit();
            }
            catch
            {
                Console.WriteLine("Error: Could not read file: " + filePath);
                Program.Exit();
            }
            string result = UnicodeEncoding.Unicode.GetString(bytes);
            return result;
        }
    }
}
