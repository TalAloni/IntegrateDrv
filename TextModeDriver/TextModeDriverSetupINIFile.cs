using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace IntegrateDrv
{
    // TxtSetup.oem File Format: http://msdn.microsoft.com/en-us/library/ff553509%28v=vs.85%29.aspx
    public class TextModeDriverSetupINIFile : INIFile
    {
        List<KeyValuePair<string, string>> m_devices = null;

        public TextModeDriverSetupINIFile() : base("txtsetup.oem")
        {
        }

        public string GetDirectoryOfDisk(string diskName)
        {
            List<string> section = GetSection("Disks");
            foreach (string line in section)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                if (keyAndValues.Key == diskName)
                {
                    string directory = keyAndValues.Value[2];
                    return directory;
                }
            }

            return String.Empty;
        }

        public List<string> GetDriverFilesSection(string deviceID)
        {
            string sectionName = String.Format("Files.scsi.{0}", deviceID);
            return GetSection(sectionName);
        }

        public List<string> GetHardwareIdsSection(string deviceID)
        {
            string sectionName = String.Format("HardwareIds.scsi.{0}", deviceID);
            return GetSection(sectionName);
        }

        public List<string> GetConfigSection(string driverKey)
        {
            string sectionName = String.Format("Config.{0}", driverKey);
            return GetSection(sectionName);
        }

        public string GetDeviceName(string deviceID)
        {
            foreach (KeyValuePair<string, string> keyAndValue in this.Devices)
            {
                if (keyAndValue.Key.Equals(deviceID))
                {
                    return keyAndValue.Value;
                }
            }
            return String.Empty;
        }

        /// <summary>
        /// KeyValuePair contains Device ID, Device Name
        /// </summary>
        public List<KeyValuePair<string,string>> Devices
        { 
            get
            {
                if (m_devices == null)
                {
                    List<string> section = GetSection("scsi");
                    m_devices = new List<KeyValuePair<string, string>>();
                    foreach (string line in section)
                    {
                        KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                        if (keyAndValues.Value.Count > 0)
                        {
                            string deviceName = Unquote(keyAndValues.Value[0]);
                            m_devices.Add(new KeyValuePair<string, string>(keyAndValues.Key, deviceName));
                        }
                    }
                }
                return m_devices;
            }
        }

        public static RegistryValueKind GetRegistryValueKind(string valueTypeString)
        {
            switch (valueTypeString)
            {
                case "REG_DWORD":
                    return RegistryValueKind.DWord;
                case "REG_QWORD":
                    return RegistryValueKind.QWord;
                case "REG_BINARY":
                    return RegistryValueKind.Binary;
                case "REG_SZ":
                    return RegistryValueKind.String;
                case "REG_EXPAND_SZ":
                    return RegistryValueKind.ExpandString; ;
                case "REG_MULTI_SZ":
                    return RegistryValueKind.MultiString;
                default:
                    return RegistryValueKind.Unknown;
            }
        }
    }
}
