using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class ServiceINIFile : INIFile
    {
        public ServiceINIFile(string fileName) : base(fileName)
        { }

        public void SetServiceToBootStart(string serviceInstallSectionName)
        {
            string line;
            int lineIndex = GetLineIndexByKey(serviceInstallSectionName, "StartType", out line);
            int valueStartIndex = line.IndexOf("=") + 1;
            string startTypeStr = GetCommaSeparatedValues(line.Substring(valueStartIndex))[0].Trim();
			if (startTypeStr.StartsWith("0x"))
			{
				startTypeStr = startTypeStr.Substring(2);
			}
			int startType = Conversion.ToInt32(startTypeStr, -1);
            if (startType != 0) // do not modify .inf that already has StartType set to 0, as it might break its digital signature unnecessarily.
            {
                line = line.Substring(0, valueStartIndex) + " 0 ;SERVICE_BOOT_START";
                UpdateLine(lineIndex, line);
            }
        }

        public void SetServiceLoadOrderGroup(string serviceInstallSectionName, string loadOrderGroup)
        {
            string line;
            int lineIndex = GetLineIndexByKey(serviceInstallSectionName, "LoadOrderGroup", out line);
            if (lineIndex >= 0)
            {
                int valueStartIndex = line.IndexOf("=") + 1;
                string existingLoadOrderGroup = GetCommaSeparatedValues(line.Substring(valueStartIndex))[0].Trim();
                if (!String.Equals(loadOrderGroup, existingLoadOrderGroup, StringComparison.InvariantCultureIgnoreCase)) // do not modify .inf that already has StartType set to 0, as it might break its digital signature unnecessarily.
                {
                    line = line.Substring(0, valueStartIndex) + " " + loadOrderGroup;
                    UpdateLine(lineIndex, line);
                }
            
            }
            else
            {
                line = "LoadOrderGroup = " + loadOrderGroup;
                AppendLineToSection(serviceInstallSectionName, line);
            }
        }
    }
}
