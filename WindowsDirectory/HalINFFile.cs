using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class HalINFFile : ServiceINIFile
    {
        public HalINFFile() : base("hal.inf")
        {}

        /// <summary>
        /// This will delete the NetCfgInstanceId value from the network adapter software key during the HAL installation phase in the beginning of GUI mode setup
        /// (not to be confused with the NetCfgInstanceId stored under software-key\Linkage which Windows actually uses)
        /// </summary>
        // AFAIK, hal.inf is the best place to perform this operation
        // (machine.inf is unsuitable because it will cause GUI-mode setup to prompt "Found new hardware" for terminal server drivers, because of signing issues)
        public void DeleteNetCfgInstanceIdFromNetworkAdapterClassInstance(string classInstanceID)
        {
            bool exist = false;
            // ClassInstall32 will only get executed during GUI-Mode setup, and not during subsequent HAL upgrades (perfect for our needs)
            string delRegSectionName = "NetCfgInstanceId.DelReg";
            string delRegDirective = "DelReg = " + delRegSectionName;

            int lineIndex = GetLineIndex("ClassInstall32", delRegDirective);
            if (lineIndex == -1)
            {
                AppendLineToSection("ClassInstall32", delRegDirective);
            }
            else
            {
                exist = true;
            }

            string entry = String.Format("HKLM,System\\CurrentControlSet\\Control\\Class\\{0}\\{1},\"NetCfgInstanceId\"", PNPDriverINFFile.NetworkAdapterClassGUID, classInstanceID);

            if (!exist)
            {
                AppendLine(";Added by IntegrateDRV");
                AppendLine("[" + delRegSectionName + "]");
                AppendLine(entry);
            }
            else
            {
                // make sure existing section deletes this classInstanceID too, or else add it to the section
                int entryIndex = GetLineIndexByKey(delRegSectionName, entry);
                if (entryIndex == -1)
                {
                    AppendLineToSection(delRegSectionName, entry);
                }
            }
        }
    }
}
