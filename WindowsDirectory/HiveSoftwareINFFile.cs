using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class HiveSoftwareINFFile : HiveINIFile
    {
        public HiveSoftwareINFFile() : base("hivesft.inf")
        { 
        }

        public HiveSoftwareINFFile(string fileName) : base(fileName)
        {
        }

        /// <summary>
        /// Should return 'Microsoft Windows 2000', 'Microsoft Windows XP' or 'Microsoft Windows Server 2003'
        /// </summary>
        public string GetWindowsProductName()
        { 
            string hive = "HKLM";
            string subKeyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string valueName = "ProductName";
            string productName = GetRegistryValueData(hive, subKeyName, valueName);
            return productName;
        }

        virtual public void RegisterDriverDirectory(string driverDirectoryWinnt)
        {
            string subKeyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion";
            string directory = String.Format(@"%SystemRoot%\{0}", driverDirectoryWinnt);
            IncludeDirectoryInDevicePath(subKeyName, directory);
        }

        protected void IncludeDirectoryInDevicePath(string subKeyName, string directory)
        {
            string hive = "HKLM";
            string valueName = "DevicePath";
            string path = GetRegistryValueData(hive, subKeyName, valueName);
            List<string> directories = StringUtils.Split(path, ';');
            if (!directories.Contains(directory))
            { 
                //directories.Add(directory);
                directories.Insert(0, directory); // added directories should have higher priority than %SystemRoot%\inf
            }
            path = StringUtils.Join(directories,";");
            path = Quote(path);
            UpdateRegistryValueData(hive, subKeyName, valueName, path);
            this.IsModified = true;
        }

        
    }
}
