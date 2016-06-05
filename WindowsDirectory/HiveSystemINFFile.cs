using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class HiveSystemINFFile : HiveINIFile, ISystemRegistryHive
    {
        public HiveSystemINFFile() : base("hivesys.inf")
        { 
        }

        /// <summary>
        /// Should return 'WinNt' or 'ServerNT' (AFAIK 'LanmanNT' is only set when a domain controller is being configured)
        /// </summary>
        public string GetWindowsProductType()
        {
            string hive = "HKLM";
            string subKeyName = @"SYSTEM\CurrentControlSet\Control\ProductOptions";
            string valueName = "ProductType";
            string productType = GetRegistryValueData(hive, subKeyName, valueName);
            return productType;
        }

        public int GetWindowsServicePackVersion()
        {
            string hive = "HKLM";
            string subKeyName = @"SYSTEM\CurrentControlSet\Control\Windows";
            string valueName = "CSDVersion";
            string CSDVersionString = GetRegistryValueData(hive, subKeyName, valueName);
            // The values of CSDVersion will be 0x100 for Service Pack 1, 0x200 for Service Pack 2, and so forth.
            return Convert.ToInt32(CSDVersionString, 16) >> 8;
        }

        public void SetCurrentControlSetRegistryKey(string keyName, string subKeyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            if (subKeyName != String.Empty)
            {
                keyName = keyName + @"\" + subKeyName;
            }
            SetCurrentControlSetRegistryKey(keyName, valueName, valueKind, valueData);
        }

        public void SetCurrentControlSetRegistryKey(string keyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            SetRegistryKey(@"SYSTEM\CurrentControlSet", keyName, valueName, valueKind, valueData);
        }

        public void SetServiceRegistryKey(string serviceName, string subKeyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            string keyName = @"Services\" + serviceName;
            SetCurrentControlSetRegistryKey(keyName, subKeyName, valueName, valueKind, valueData);
        }

        public void SetRegistryKey(string keyName, string subKeyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            if (subKeyName != String.Empty)
            {
                keyName += "\\" + subKeyName;
            }
            SetRegistryKey(keyName, valueName, valueKind, valueData);
        }

        public void SetRegistryKey(string keyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            SetRegistryKeyInternal(keyName, valueName, valueKind, GetFormattedValueData(valueData, valueKind));
        }

        // Internal should be used only by methods that pass properly formatted valueData or methods that reads directry from .inf
        /// <param name="valueData">string input must be quoted</param>
        private void SetRegistryKeyInternal(string keyName, string valueName, RegistryValueKind valueKind, string valueData)
        {
            string valueTypeHexString = GetRegistryValueTypeHexString(valueKind);
            string hive = "HKLM";
            
            string lineFound;
            int lineIndex = this.GetLineStartIndex("AddReg", hive, keyName, valueName, out lineFound);
            string line = String.Format("{0},\"{1}\",\"{2}\",{3},{4}", hive, keyName, valueName, valueTypeHexString, valueData);
            if (lineIndex == -1) // add line
            {
                AppendLineToSection("AddReg", line);
            }
            else // update line
            {
                UpdateLine(lineIndex, line);
            }
        }

        public List<string> GetServiceGroupOrderEntry()
        { 
            string hive = "HKLM";
            string keyName = @"SYSTEM\CurrentControlSet\Control\ServiceGroupOrder";
            string valueName="List";
            string serviceGroupOrderStringList = GetRegistryValueData(hive, keyName, valueName);
            List<string> serviceGroupOrderList = GetCommaSeparatedValues(serviceGroupOrderStringList);
            for (int index = 0; index < serviceGroupOrderList.Count; index++)
            {
                serviceGroupOrderList[index] = Unquote(serviceGroupOrderList[index]);
            }
            return serviceGroupOrderList;
        }

        public void SetServiceGroupOrderEntry(List<string> serviceGroupOrder)
        { 
            string hive = "HKLM";
            string keyName = @"SYSTEM\CurrentControlSet\Control\ServiceGroupOrder";
            string valueName="List";

            string valueData = GetFormattedMultiString(serviceGroupOrder.ToArray());

            UpdateRegistryValueData(hive, keyName, valueName, valueData);
        }

        public void AddServiceGroupsAfterSystemBusExtender(List<string> serviceGroupNames)
        {
            List<string> serviceGroupOrder = GetServiceGroupOrderEntry();

            // remove existing entry (because it may be in the wrong place)
            foreach (string serviceGroupName in serviceGroupNames)
            {
                int index = StringUtils.IndexOfCaseInsensitive(serviceGroupOrder, serviceGroupName);
                if (index != -1)
                {
                    serviceGroupOrder.RemoveAt(index);
                }
            }

            // add entry
            if (serviceGroupOrder.Count > 3)
            {
                serviceGroupOrder.InsertRange(3, serviceGroupNames);
            }
            else
            {
                Console.WriteLine("Critical warning: hivesys.inf has been tampered with");
            }

            SetServiceGroupOrderEntry(serviceGroupOrder);
        }

        public void AddDeviceToCriticalDeviceDatabase(string hardwareID, string serviceName)
        {
            AddDeviceToCriticalDeviceDatabase(hardwareID, serviceName, String.Empty);
        }

        public void AddDeviceToCriticalDeviceDatabase(string hardwareID, string serviceName, string classGUID)
        {
            hardwareID = hardwareID.Replace(@"\", "#");
            hardwareID = hardwareID.ToLower();
            SetCurrentControlSetRegistryKey(@"Control\CriticalDeviceDatabase\" + hardwareID, "Service", RegistryValueKind.String, serviceName);
            if (classGUID != String.Empty)
            {
                SetCurrentControlSetRegistryKey(@"Control\CriticalDeviceDatabase\" + hardwareID, "ClassGUID", RegistryValueKind.String, classGUID);
            }
        }

        public string AllocateVirtualDeviceInstanceID(string deviceClassName)
        {
            string keyName = @"SYSTEM\CurrentControlSet\Enum\Root\" + deviceClassName;
            return AllocateNumericInstanceID(keyName);
        }

        private string AllocateNumericInstanceID(string keyName)
        {
            int instanceIDInt = 0;
            string instanceID = instanceIDInt.ToString("0000");
            while(ContainsKey("HKLM", keyName + @"\" + instanceID))
            {
                instanceIDInt++;
                instanceID = instanceIDInt.ToString("0000");
            }
            return instanceID;
        }
    }
}
