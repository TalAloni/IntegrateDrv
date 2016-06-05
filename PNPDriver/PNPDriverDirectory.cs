using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class PNPDriverDirectory
    {
        private string m_path = String.Empty;
        private List<PNPDriverINFFile> m_infList;
        List<KeyValuePair<string, string>> m_devices = null; // this include list of the devices from all the INFs in the directory

        public PNPDriverDirectory(string path)
        {
            m_path = path;
            
            List<string> fileNames = GetINFFileNamesInDirectory(path);
            m_infList = new List<PNPDriverINFFile>();
            foreach (string fileName in fileNames)
            {
                PNPDriverINFFile driverInf = new PNPDriverINFFile(fileName);
                driverInf.ReadFromDirectory(path);
                m_infList.Add(driverInf);
            }
        }

        public bool ContainsINFFiles
        {
            get
            {
                return (m_infList.Count > 0);
            }
        }

        private static List<string> GetINFFileNamesInDirectory(string path)
        { 
            List<string> result = new List<string>();
            string[] filePaths = new string[0];
            try
            {
                filePaths = Directory.GetFiles(path, "*.inf");
            }
            catch (DirectoryNotFoundException)
            {

            }
            catch (ArgumentException) // such as "Path contains invalid chars"
            {
                
            }

            foreach (string filePath in filePaths)
            {
                string fileName = FileSystemUtils.GetNameFromPath(filePath);
                result.Add(fileName);
            }
            return result;
        }

        public string GetDeviceInstallSectionName(string hardwareIDToFind, string architectureIdentifier, int minorOSVersion, int productType, out PNPDriverINFFile pnpDriverInf)
        {
            foreach (PNPDriverINFFile driverInf in m_infList)
            {
                string installSectionName = driverInf.GetDeviceInstallSectionName(hardwareIDToFind, architectureIdentifier, minorOSVersion, productType);
                if (installSectionName != String.Empty)
                {
                    pnpDriverInf = driverInf;
                    return installSectionName;
                }
            }
            pnpDriverInf = null;
            return String.Empty;
        }

        public List<KeyValuePair<string, string>> ListDevices(string architectureIdentifier, int minorOSVersion, int productType)
        {
            if (m_devices == null)
            {
                m_devices = new List<KeyValuePair<string, string>>();
                foreach (PNPDriverINFFile driverInf in m_infList)
                {
                    m_devices.AddRange(driverInf.ListDevices(architectureIdentifier, minorOSVersion, productType));
                }
            }
            return m_devices;
        }

        public bool ContainsRootDevices(string architectureIdentifier, int minorOSVersion, int productType)
        {
            foreach (PNPDriverINFFile driverInf in m_infList)
            {
                if (driverInf.ContainsRootDevices(architectureIdentifier, minorOSVersion, productType))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsNetworkAdapter
        {
            get
            {
                foreach (PNPDriverINFFile driverInf in m_infList)
                {
                    if (driverInf.IsNetworkAdapter)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public string Path
        {
            get
            {
                return m_path;
            }
        }
    }
}
