using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class HiveSoftware32INFFile : HiveSoftwareINFFile
    {
        public HiveSoftware32INFFile() : base("hivsft32.inf")
        { }

        public override void RegisterDriverDirectory(string driverDirectoryWinnt)
        {
            string subKeyName = "SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion";
            string directory = String.Format("%SystemRoot%\\{0}", driverDirectoryWinnt);
            IncludeDirectoryInDevicePath(subKeyName, directory);
        }
    }
}
