using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace IntegrateDrv
{
    public class HiveINIFile : INIFile
    {
        public HiveINIFile() : base()
        { }

        public HiveINIFile(string fileName) : base(fileName)
        { }

        protected string GetRegistryValueData(string hive, string keyName, string valueName)
        {
            string lineStart = String.Format("{0},\"{1}\",\"{2}\"", hive, keyName, valueName);
            string line;
            int lineIndex = GetLineStartIndex("AddReg", lineStart, out line);
            if (lineIndex >= 0)
            {
                int valueDataStartIndex = line.Substring(lineStart.Length + 1).IndexOf(",") + lineStart.Length + 2;
                string hexStringValueTypeFlags = line.Substring(lineStart.Length + 1, valueDataStartIndex - lineStart.Length - 2);
                string valueData = line.Substring(valueDataStartIndex);
                RegistryValueKind valueKind = PNPDriverINFFile.GetRegistryValueKind(hexStringValueTypeFlags);
                if (valueKind == RegistryValueKind.MultiString)
                {
                    return valueData;
                }
                else
                {
                    return Unquote(valueData);
                }
            }
            else
            {
                return String.Empty;
            }
        }

        protected void UpdateRegistryValueData(string hive, string keyName, string valueName, string valueData)
        {
            string lineStart = String.Format("{0},\"{1}\",\"{2}\"", hive, keyName, valueName);
            string line;
            int lineIndex = GetLineStartIndex("AddReg", hive, keyName, valueName, out line);
             
            if (lineIndex >= 0)
            {
                int valueDataStartIndex = line.Substring(lineStart.Length + 1).IndexOf(",") + lineStart.Length + 2;

                string updatedLine = line.Substring(0, valueDataStartIndex) + valueData;
                this.UpdateLine(lineIndex, updatedLine, true);
            }
            else
            {
                Console.WriteLine("Error: '{0}' key was not found in {1}!", valueName, this.FileName);
                Program.Exit();
            }
        }

        public bool ContainsKey(string hive, string keyName)
        {
            string lineStart = String.Format("{0},\"{1}\"", hive, keyName);
            int lineIndex = GetLineStartIndex("AddReg", lineStart);
            if (lineIndex != -1)
            {
                return true;
            }
            lineStart = String.Format("{0},\"{1}\\", hive, keyName);
            lineIndex = GetLineStartIndex("AddReg", lineStart);
            return (lineIndex != -1);
        }

        protected int GetLineStartIndex(string sectionName, string hive, string subKeyName, string valueName)
        { 
            string lineFound;
            return GetLineStartIndex(sectionName, hive, subKeyName, valueName, out lineFound);
        }

        protected int GetLineStartIndex(string sectionName, string hive, string subKeyName, string valueName, out string lineFound)
        {
            string lineStart = String.Format("{0},\"{1}\",\"{2}\"", hive, subKeyName, valueName);
            return GetLineStartIndex(sectionName, lineStart, out lineFound);
        }

        protected int GetLineStartIndex(string sectionName, string lineStart)
        { 
            string lineFound;
            return GetLineStartIndex(sectionName, lineStart, out lineFound);
        }

        protected int GetLineStartIndex(string sectionName, string lineStart, out string lineFound)
        {
            Predicate<string> lineStartsWith = delegate(string line) { return line.Trim().StartsWith(lineStart, StringComparison.InvariantCultureIgnoreCase); };
            return GetLineIndex(sectionName, lineStartsWith, out lineFound, true);
        }

        public static object ParseValueDataString(string valueData, RegistryValueKind valueKind)
        {
            switch (valueKind)
            {
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    return INIFile.Unquote(valueData);
                case RegistryValueKind.MultiString:
                    List<string> stringList = INIFile.GetCommaSeparatedValues(valueData);
                    for (int index = 0; index < stringList.Count; index++)
                    {
                        stringList[index] = INIFile.Unquote(stringList[index]);
                        stringList[index] = stringList[index].Replace("\"\"", "\""); // see notes at GetFormattedMultiString()
                    }
                    return stringList.ToArray();
                case RegistryValueKind.DWord:
                    // Sometimes a DWord value is quoted (Intel E1000 driver, version 8.10.3.0)
                    // It's a violation of the specs, but Windows accepts this, so we should too.
                    valueData = Unquote(valueData);
                    return Convert.ToInt32(valueData, 16); // DWord values are in Hex
                case RegistryValueKind.QWord:
                    return Convert.ToInt64(valueData, 16);  // QWord values are in Hex
                case RegistryValueKind.Binary:
                    List<string> byteStringList = INIFile.GetCommaSeparatedValues(valueData);
                    List<byte> byteList = new List<byte>();
                    for (int index = 0; index < byteStringList.Count; index++)
                    {
                        // Sometimes each byte value is quoted (VIA Rhine III Fast Ethernet Adapter driver, version 3.41.0.0426)
                        // It's a violation of the specs, but Windows accepts this, so we should too.
                        string byteString = Unquote(byteStringList[index]);
                        byte data = Convert.ToByte(byteString, 16); // byte values are in Hex
                        byteList.Add(data);
                    }
                    return byteList.ToArray();
                default:
                    throw new NotImplementedException("Not implemented");
            }
        }

        // http://msdn.microsoft.com/en-us/library/ff546320%28v=VS.85%29.aspx
        protected string GetRegistryValueTypeHexString(RegistryValueKind valueKind)
        {
            switch (valueKind)
            {
                case RegistryValueKind.DWord:
                    return "0x00010001";
                case RegistryValueKind.String:
                    return "0x00000000";
                case RegistryValueKind.ExpandString:
                    return "0x00020000";
                case RegistryValueKind.Binary:
                    return "0x00000001";
                case RegistryValueKind.MultiString:
                    return "0x00010000";
                case RegistryValueKind.QWord:
                    throw new Exception("QWord is not supported"); // the specs do not list a value for QWORD
                default:
                    return "0x00020001"; //FLG_ADDREG_TYPE_NONE
            }
        }

        public static string GetFormattedValueData(object valueData, RegistryValueKind valueKind)
        {
            switch (valueKind)
            {
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    if (valueData is String)
                    {
                        return Quote((string)valueData);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid argument supplied");
                    }
                case RegistryValueKind.MultiString:
                    if (valueData is string[])
                    {
                        return GetFormattedMultiString((string[])valueData);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid argument supplied");
                    }
                case RegistryValueKind.DWord:
                    if (valueData is int || valueData is uint)
                    {
                        return valueData.ToString();
                    }
                    else
                    {
                        throw new ArgumentException("Invalid argument supplied");
                    }
                case RegistryValueKind.QWord:
                    if (valueData is long || valueData is ulong)
                    {
                        return valueData.ToString();
                    }
                    else
                    {
                        throw new ArgumentException("Invalid argument supplied");
                    }
                case RegistryValueKind.Binary:
                    if (valueData is byte[])
                    {
                        return GetFormattedBinary((byte[])valueData);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid argument supplied");
                    }
                default:
                    throw new ArgumentException("Invalid argument supplied");
            }
        }

        // http://msdn.microsoft.com/en-us/library/ff547485.aspx
        // http://rubli.info/t-blog/2008/08/05/quoting-strings-in-inf-addreg-sections/
        public static string GetFormattedMultiString(string[] array)
        {
            if (array.Length == 0)
            {
                return Quote(String.Empty);
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < array.Length; index++)
            {
                if (index != 0)
                {
                    builder.Append(",");
                }
                // each string is treated separately
                string quote = '"'.ToString();
                string formatted = array[index].Replace(quote, quote + quote);
                builder.Append(Quote(formatted));
            }
            return builder.ToString();
        }

        public static string GetFormattedBinary(byte[] array)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < array.Length; index++)
            {
                if (index != 0)
                {
                    builder.Append(",");
                }
                builder.Append(array[index].ToString("X2")); // 2 digit hex
            }
            return builder.ToString();
        }
    }
}
