using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class PNPLocalHardwareDetector
    {
        public static List<string> DetectMatchingLocalHardware(PNPDriverDirectory driverDirectory, string architectureIdentifier, int minorOSVersion, int productType)
        {
            List<KeyValuePair<string, string>> devices = driverDirectory.ListDevices(architectureIdentifier, minorOSVersion, productType);
            List<string> driverHardwareID = new List<string>();
            foreach (KeyValuePair<string, string> device in devices)
            {
                driverHardwareID.Add(device.Key);
            }
            return DetectMatchingLocalHardware(driverHardwareID);
        }

        // driverHardwareID can be different than the actuall matching hardware ID,
        // for example: driver hardware ID can be VEN_8086&DEV_100F, while the actuall hardware may present VEN_8086&DEV_100F&SUBSYS...
        /// <returns>List of driverHardwareID that match list of hardware</returns>
        public static List<string> DetectMatchingLocalHardware(List<string> driverHardwareIDs)
        {
            List<string> localHardwareIDs = GetLocalHardwareCompatibleIDs();
            List<string> result = new List<string>();
            foreach (string driverHardwareID in driverHardwareIDs)
            {
                if (StringUtils.ContainsCaseInsensitive(localHardwareIDs, driverHardwareID)) // localHardwareIDs is sometimes upcased by Windows
                {
                    result.Add(driverHardwareID);
                }
            }
            return result;
        }

        /// <param name="hardwareID">enumerator-specific-device-id</param>
        public static string DetectLocalDeviceInstanceID(string hardwareID, out string deviceID)
        {
            Console.WriteLine("Searching for '" + hardwareID + "' on local machine");
            deviceID = String.Empty; // sometimes the device presents longer hardware ID than the one specified in the driver

            string enumerator = PNPDriverIntegratorUtils.GetEnumeratorNameFromHardwareID(hardwareID);
            if (enumerator == "*")
            {
                return String.Empty; // unsupported enumerator;
            }

            string deviceInstanceID = String.Empty;
            RegistryKey hiveKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + enumerator);

            foreach (string deviceKeyName in hiveKey.GetSubKeyNames())
            {
                RegistryKey deviceKey = hiveKey.OpenSubKey(deviceKeyName);
                if (deviceKey != null)
                {
                    foreach (string instanceKeyName in deviceKey.GetSubKeyNames())
                    {
                        RegistryKey instanceKey = deviceKey.OpenSubKey(instanceKeyName);
                        if (instanceKey != null)
                        {
                            List<string> hardwareIDs = new List<string>();

                            object hardwareIDEntry = instanceKey.GetValue("HardwareID", new string[0]);
                            if (hardwareIDEntry is string[])
                            {
                                hardwareIDs.AddRange((string[])hardwareIDEntry);
                            }

                            object compatibleIDsEntry = instanceKey.GetValue("CompatibleIDs", new string[0]);
                            if (compatibleIDsEntry is string[])
                            {
                                hardwareIDs.AddRange((string[])compatibleIDsEntry);
                            }

                            if (StringUtils.ContainsCaseInsensitive(hardwareIDs, hardwareID))
                            {
                                deviceID = RegistryKeyUtils.GetShortKeyName(deviceKey.Name);
                                deviceInstanceID = RegistryKeyUtils.GetShortKeyName(instanceKey.Name);
                                // Irrelevant Note: if a device is present but not installed in Windows then ConfigFlags entry will not be present
                                // and it doesn't matter anyway because we don't care about how existing installation configure the device

                                // there are two reasons not to use DeviceDesc from the local machine:
                                // 1. on Windows 6.0+ (or just Windows PE?) the format is different and not compatible with Windows 5.x
                                // 2. If the hadrware is present but not installed, the DeviceDesc will be a generic description (e.g. 'Ethernet Controller')

                                Console.WriteLine("Found matching device: '" + deviceID + "'");
                                instanceKey.Close();
                                deviceKey.Close();
                                hiveKey.Close();
                                return deviceInstanceID;
                            }
                            
                            instanceKey.Close();
                        }
                    }
                    deviceKey.Close();
                }
            }

            hiveKey.Close();
            return deviceInstanceID;
        }

        public static List<string> GetLocalHardwareCompatibleIDs()
        {
            List<string> result = new List<string>();
            RegistryKey hiveKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum");

            foreach (string enumerator in hiveKey.GetSubKeyNames())
            {
                RegistryKey enumeratorKey = hiveKey.OpenSubKey(enumerator);
                if (enumeratorKey != null)
                {
                    foreach (string deviceKeyName in enumeratorKey.GetSubKeyNames())
                    {
                        RegistryKey deviceKey = enumeratorKey.OpenSubKey(deviceKeyName);
                        if (deviceKey != null)
                        {
                            foreach (string instanceKeyName in deviceKey.GetSubKeyNames())
                            {
                                RegistryKey instanceKey = deviceKey.OpenSubKey(instanceKeyName);
                                if (instanceKey != null)
                                {
                                    object hardwareIDEntry = instanceKey.GetValue("HardwareID", new string[0]);
                                    if (hardwareIDEntry is string[])
                                    {
                                        result.AddRange((string[])hardwareIDEntry);
                                    }

                                    object compatibleIDsEntry = instanceKey.GetValue("CompatibleIDs", new string[0]);
                                    if (compatibleIDsEntry is string[])
                                    {
                                        result.AddRange((string[])compatibleIDsEntry);
                                    }
                                    instanceKey.Close();
                                }
                            }
                            deviceKey.Close();
                        }
                    }
                    enumeratorKey.Close();
                }
            }

            hiveKey.Close();
            return result;
        }
    }
}
