using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class SetupRegistryHiveFile : ISystemRegistryHive
    {
        private static readonly string m_hiveKeyName = "setupreg";
        private readonly string m_fileName = "setupreg.hiv";
        private static bool m_isLoaded = false;

        // we work on a temporary copy of the hive so that we would be able to commit the changes only after the selection process has completed,
        // this way, we are able to avoid partial integration (inconsistency) in the case of crash
        private string m_hivePath = String.Empty;
        private static string m_tempHivePath = String.Empty;

        public SetupRegistryHiveFile()
        { 
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
            SetRegistryKey("ControlSet001", keyName, valueName, valueKind, valueData);
        }

        public void SetServiceRegistryKey(string serviceName, string subKeyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            string keyName = @"Services\" + serviceName;
            SetCurrentControlSetRegistryKey(keyName, subKeyName, valueName, valueKind, valueData);
        }

        [Obsolete]
        public void SetRegistryKeyUnparsed(string keyName, string subKeyName, string valueName, RegistryValueKind valueKind, string valueData)
        {
            SetRegistryKey(keyName, subKeyName, valueName, valueKind, HiveINIFile.ParseValueDataString(valueData, valueKind));
        }

        [Obsolete]
        public void SetRegistryKeyUnparsed(string keyName, string valueName, RegistryValueKind valueKind, string valueData)
        {
            SetRegistryKey(keyName, valueName, valueKind, HiveINIFile.ParseValueDataString(valueData, valueKind));
        }

        public void SetRegistryKey(string keyName, string subKeyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            if (subKeyName != String.Empty)
            {
                keyName += @"\" + subKeyName;
            }
            SetRegistryKey(keyName, valueName, valueKind, valueData);
        }

        public void SetRegistryKey(string keyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            if (m_isLoaded)
            {
                // currently Mono doesn't give write access this way:
                // RegistryKey hiveKey = Registry.Users.OpenSubKey(m_hiveSubKeyName, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl); // Opens the key again with full control.

                // work-around for Mono:
                RegistryKey hiveKey = Registry.Users.OpenSubKey(m_hiveKeyName, true);

                RegistryKey key = null;
                try
                {
                    key = hiveKey.CreateSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("Error: failed to create registry key: '{0}'", keyName);
                    hiveKey.Close();
                    UnloadHive(false);
                    Environment.Exit(-1);
                }

                key.SetValue(valueName, valueData, valueKind);
                key.Close();
                hiveKey.Close();
            }
            else
            {
                throw new Exception("Registry hive is not loaded");
            }
        }

        /// <summary>
        /// If a service uses the first item in the group, it tells windows that it is in text-mode setup (setupdd.sys uses the first group during text-mode)
        /// Boot Bus Extender and System Bus Extender should follow, and we will insert the new service group immediately after those three.
        /// </summary>
        public void AddServiceGroupAfterSystemBusExtender(string serviceGroupName)
        {
            if (m_isLoaded)
            {
                RegistryKey hiveKey = Registry.Users.OpenSubKey(m_hiveKeyName, true);
                string subKeyName = @"ControlSet001\Control\ServiceGroupOrder";
                string valueName = "List";
                RegistryKey key = hiveKey.OpenSubKey(subKeyName, true);
                string[] servicesArray = (string[])key.GetValue(valueName);
                List<string> serviceList = new List<string>(servicesArray);
                
                // remove existing entry (it may be in the wrong place)
                int index = StringUtils.IndexOfCaseInsensitive(serviceList, serviceGroupName);
                if (index != -1)
                {
                    serviceList.RemoveAt(index);
                }

                // add new entry at the proper place (we assume the user did not modify the location of SystemBusExtender)
                if (serviceList.Count > 3)
                {
                    serviceList.Insert(3, serviceGroupName);
                }
                else
                {
                    Console.WriteLine("Error: Registry key 'ServiceGroupOrder' is corrupted.");
                    key.Close();
                    hiveKey.Close();
                    UnloadHive(false);
                    Environment.Exit(-1);
                }

                servicesArray = serviceList.ToArray();
                key.SetValue(valueName, servicesArray, RegistryValueKind.MultiString);
                key.Close();
                hiveKey.Close();
            }
            else
            {
                throw new Exception("Registry hive is not loaded");
            }
        }

        public void AddServiceGroupsAfterSystemBusExtender(List<string> serviceGroupNames)
        {
            // we add the entries in the reverse order, so they would end up on the correct order
            serviceGroupNames.Reverse();
            foreach (string serviceGroupName in serviceGroupNames)
            {
                AddServiceGroupAfterSystemBusExtender(serviceGroupName);
            }
        }

        public string AllocateClassInstanceID(string classGUID)
        {
            string keyName = @"ControlSet001\Control\Class\" + classGUID;
            return AllocateNumericInstanceID(keyName);
        }

        public string AllocateVirtualDeviceInstanceID(string deviceClassName)
        {
            string keyName = @"ControlSet001\Enum\Root\" + deviceClassName;
            return AllocateNumericInstanceID(keyName);
        }

        private string AllocateNumericInstanceID(string keyName)
        {
            if (m_isLoaded)
            {
                RegistryKey hiveKey = Registry.Users.OpenSubKey(m_hiveKeyName, true);

                RegistryKey key = hiveKey.OpenSubKey(keyName, true);
                string instanceID;
                if (key == null)
                {
                    instanceID = "0000";
                }
                else
                {
                    List<string> deviceInstancesArray = new List<string>(key.GetSubKeyNames());
                    if (deviceInstancesArray.Count == 0)
                    {
                        instanceID = "0000";
                    }
                    else
                    {
                        int deviceInstanceIDInt = Convert.ToInt32(deviceInstancesArray[deviceInstancesArray.Count - 1]) + 1;
                        instanceID = deviceInstanceIDInt.ToString("0000");
                    }
                    key.Close();
                }
                hiveKey.Close();
                return instanceID;
            }
            else
            {
                throw new Exception("Registry hive is not loaded");
            }
        }

        /*
        public string FindVirtualDeviceInstanceID(string deviceClassName, string hardwareIDToFind)
        {
            string result = String.Empty;
            string keyName = @"ControlSet001\Enum\Root\" + deviceClassName;
            if (m_isLoaded)
            {
                RegistryKey hiveKey = Registry.Users.OpenSubKey(m_hiveKeyName);
                RegistryKey key = hiveKey.OpenSubKey(keyName);
                
                List<string> deviceInstanceIDs = new List<string>(key.GetSubKeyNames());
                foreach (string deviceInstanceID in deviceInstanceIDs)
                {
                    RegistryKey deviceInstanceKey = key.OpenSubKey(deviceInstanceID);
                    object hardwareIDEntry = deviceInstanceKey.GetValue("HardwareID", new string[0]);
                    if (hardwareIDEntry is string[])
                    {
                        string hardwareID = ((string[])hardwareIDEntry)[0];
                        if (String.Equals(hardwareID, hardwareIDToFind, StringComparison.InvariantCultureIgnoreCase))
                        {
                            result = deviceInstanceID;
                            break;
                        }
                    }
                    deviceInstanceKey.Close();
                }
                key.Close();
                hiveKey.Close();
                return result;
            }
            else
            {
                throw new Exception("Registry hive is not loaded");
            }
        }
        */

        public void LoadHiveFromDirectory(string directory)
        {
            m_hivePath = directory + m_fileName;
            // GetTempPath() should end with backward slash:
            m_tempHivePath = Path.GetTempPath() + m_fileName;
            
            try
            {
                File.Copy(m_hivePath, m_tempHivePath, true);
            }
            catch
            {
                // perhaps the hive is already loaded from a previous crash / break
                // let's try to unload it
                RegistryUtils.UnloadHive(m_hiveKeyName);
                ProgramUtils.CopyCriticalFile(m_hivePath, m_tempHivePath);
                // CopyCriticalFile will exit the program on failue
            }

            int result = RegistryUtils.LoadHive(m_hiveKeyName, m_tempHivePath);
            m_isLoaded = (result == 0);
            if (!m_isLoaded)
            {
                Console.WriteLine("Error: failed to load registry hive '{0}', error code: {1}", m_fileName, result);
                Console.WriteLine("This may happen due to one of the following reasons:");
                Console.WriteLine("- You do not have access to perform this operation");
                Console.WriteLine("- The hive is already loaded and needs to be manually unloaded (HKEY_USERS\\setupreg)");
                Environment.Exit(-1);
            }
            else
            {
                // if ControlSet001\Enum exist, its permissions will stick and stay for GUI-Mode \ final windows,
                // The default windows permissions for CurrentControlSet\Enum are:
                // SYSTEM   -> Full Control
                // Everyone -> Read
                // so we must make sure the default permissions are granted.
                string subKeyName = m_hiveKeyName + @"\ControlSet001\Enum";
                // we're creating or opening the Enum key (if it's already exist, the permissions will simply be overwritten)
                // Note that we're creating ControlSet001\Enum even if we're not using it.
                RegistryKey enumKey = Registry.Users.CreateSubKey(subKeyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                enumKey.Close();
                RegistryKeyUtils.SetHKeyUsersKeySecutiryDescriptor(subKeyName, RegistryKeyUtils.DesiredEnumSecurityDescriptorString);
            }
        }

        public void UnloadHive(bool commit)
        {
            if (m_isLoaded)
            {
                int result = RegistryUtils.UnloadHive(m_hiveKeyName);
                m_isLoaded = (result != 0);
                if (commit)
                {
                    FileSystemUtils.ClearReadOnlyAttribute(m_hivePath);
                    ProgramUtils.CopyCriticalFile(m_tempHivePath, m_hivePath);
                }
                
                try
                {
                    File.Delete(m_tempHivePath);
                }
                catch
                { 
                }

                if (m_isLoaded)
                {
                    Console.WriteLine("Warning: failed to unload setup registry hive [Code: {0}]", result);
                }
            }
        }

        /*
        private RegistryValueKind GetRegistryValueKind(string valueType)
        {
            switch (valueType)
            {
                case "REG_DWORD":
                    return RegistryValueKind.DWord;
                case "REG_SZ":
                    return RegistryValueKind.String;
                case "REG_EXPAND_SZ":
                    return RegistryValueKind.ExpandString;
                case "REG_BINARY":
                    return RegistryValueKind.Binary;
                case "REG_MULTI_SZ":
                    return RegistryValueKind.MultiString;
                default:
                    return RegistryValueKind.Unknown;
            }
        }*/

        public string FileName
        {
            get
            {
                return m_fileName;
            }
        }
    }
}
