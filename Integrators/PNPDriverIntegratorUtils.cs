using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class PNPDriverIntegratorUtils
    {
        public static string UISelectHardwareID(PNPDriverDirectory pnpDriverDirectory, WindowsInstallation installation, bool useLocalHardwareConfig, string enumExportPath)
        {
            string hardwareID;
            bool containsRootDevices = pnpDriverDirectory.ContainsRootDevices(installation.ArchitectureIdentifier, installation.MinorOSVersion, installation.ProductType);
            // We should not use our detection mechanism if a driver directory contains a root device.
            if (!containsRootDevices && (useLocalHardwareConfig || enumExportPath != String.Empty))
            {
                List<string> matchingHardwareIDs;
                if (useLocalHardwareConfig)
                {
                    matchingHardwareIDs = PNPLocalHardwareDetector.DetectMatchingLocalHardware(pnpDriverDirectory, installation.ArchitectureIdentifier, installation.MinorOSVersion, installation.ProductType);
                }
                else
                {
                    matchingHardwareIDs = PNPExportedHardwareDetector.DetectMatchingExportedHardware(enumExportPath, pnpDriverDirectory, installation.ArchitectureIdentifier, installation.MinorOSVersion, installation.ProductType);
                }
                List<KeyValuePair<string, string>> devices = pnpDriverDirectory.ListDevices(installation.ArchitectureIdentifier, installation.MinorOSVersion, installation.ProductType);
                // We now have a list of hardware IDs that matches (some of) our devices, let's found out which of the devices match
                List<KeyValuePair<string, string>> matchingDevices = new List<KeyValuePair<string, string>>();
                foreach (KeyValuePair<string, string> device in devices)
                {
                    if (matchingHardwareIDs.Contains(device.Key))
                    {
                        matchingDevices.Add(device);
                    }
                }
                Console.WriteLine();
                Console.WriteLine("Looking for matching device drivers in directory '{0}':", pnpDriverDirectory.Path);
                hardwareID = UISelectMatchingHardwareID(matchingDevices);
            }
            else
            {
                hardwareID = UISelectMatchingHardwareID(pnpDriverDirectory, installation.ArchitectureIdentifier, installation.MinorOSVersion, installation.ProductType);
            }

            return hardwareID;
        }

        public static string UISelectMatchingHardwareID(PNPDriverDirectory driverDirectory, string architectureIdentifier, int minorOSVersion, int productType)
        {
            List<KeyValuePair<string, string>> devices = driverDirectory.ListDevices(architectureIdentifier, minorOSVersion, productType);
            Console.WriteLine();
            Console.WriteLine("Looking for matching device drivers in directory '{0}':", driverDirectory.Path);
            return UISelectMatchingHardwareID(devices);
        }

        public static string UISelectMatchingHardwareID(List<KeyValuePair<string, string>> devices)
        {
            string hardwareID = String.Empty;

            if (devices.Count > 1)
            {
                Console.WriteLine("Found matching device drivers for the following devices:");
                for (int index = 0; index < devices.Count; index++)
                {
                    int oneBasedIndex = index + 1;
                    Console.WriteLine("{0}. {1}", oneBasedIndex.ToString("00"), devices[index].Value);
                    Console.WriteLine("    Hardware ID: " + devices[index].Key);
                }
                Console.Write("Select the device driver you wish to integrate: ");
                // driver number could be double-digit, so we use ReadLine()
                int selection = Conversion.ToInt32(Console.ReadLine()) - 1;
                if (selection >= 0 && selection < devices.Count)
                {
                    hardwareID = devices[selection].Key;
                    return hardwareID;
                }
                else
                {
                    Console.WriteLine("Error: No device has been selected, exiting.");
                    return String.Empty;
                }
            }
            else if (devices.Count == 1)
            {
                hardwareID = devices[0].Key;
                string deviceName = devices[0].Value;
                Console.WriteLine("Found one matching device driver:");
                Console.WriteLine("1. " + deviceName);
                return hardwareID;
            }
            else
            {
                Console.WriteLine("No matching device drivers have been found.");
                return String.Empty;
            }
        }

        public static List<DeviceService> IntegratePNPDrivers(List<PNPDriverDirectory> pnpDriverDirectories, WindowsInstallation installation, bool useLocalHardwareConfig, string enumExportPath, bool preconfigure)
        {
            List<DeviceService> deviceServices = new List<DeviceService>();
            foreach (PNPDriverDirectory pnpDriverDirectory in pnpDriverDirectories)
            {
                string hardwareID = UISelectHardwareID(pnpDriverDirectory, installation, useLocalHardwareConfig, enumExportPath);
                
                if (hardwareID == String.Empty)
                {
                    // No device has been selected, exit.
                    // UISelectDeviceID has already printed an error message
                    Program.Exit();
                }

                Console.WriteLine("Integrating PNP driver for '" + hardwareID + "'");
                PNPDriverIntegrator integrator = new PNPDriverIntegrator(pnpDriverDirectory, installation, hardwareID, useLocalHardwareConfig, enumExportPath, preconfigure);
                integrator.IntegrateDriver();
                deviceServices.AddRange(integrator.DeviceServices);
            }
            return deviceServices;
        }

        /// <param name="subdir">Presumably in the following form: '\x86'</param>
        public static string GeSourceFileDiskID(PNPDriverINFFile pnpDriverInf, string sourceFileName, string architectureIdentifier, out string subdir)
        {
            // During installation, SetupAPI functions look for architecture-specific SourceDisksFiles sections before using the generic section
            string platformSpecificSectionName = "SourceDisksFiles." + architectureIdentifier;
            List<string> values = pnpDriverInf.GetValuesOfKeyInSection(platformSpecificSectionName, sourceFileName);
            if (values.Count == 0)
            {
                values = pnpDriverInf.GetValuesOfKeyInSection("SourceDisksFiles", sourceFileName);
            }
            // filename=diskid[,[ subdir][,size]]
            string diskID = INIFile.TryGetValue(values, 0);
            subdir = INIFile.TryGetValue(values, 1);
            return diskID;
        }

        /// <returns>
        /// Null if the diskID entry was not found,
        /// otherwise, the path is supposed to be in the following form: '\WinNT'
        /// </returns>
        public static string GeSourceDiskPath(PNPDriverINFFile pnpDriverInf, string diskID, string architectureIdentifier)
        {
            List<string> values = pnpDriverInf.GetValuesOfKeyInSection("SourceDisksNames." + architectureIdentifier, diskID);
            if (values.Count == 0)
            {
                values = pnpDriverInf.GetValuesOfKeyInSection("SourceDisksNames", diskID);
            }

            if (values.Count > 0)
            {
                // diskid = disk-description[,[tag-or-cab-file],[unused],[path],[flags][,tag-file]]
                string path = INIFile.TryGetValue(values, 3);
                // Quoted path is allowed (example: SiS 900-Based PCI Fast Ethernet Adapter driver, version 2.0.1039.1190)
                return QuotedStringUtils.Unquote(path);
            }
            else
            {
                return null;
            }
        }

        public static string GetRelativeSourceFilePath(PNPDriverINFFile pnpDriverInf, string sourceFileName, string architectureIdentifier)
        {
            string relativeDirectoryPath = GetRelativeDirectoryPath(pnpDriverInf, sourceFileName, architectureIdentifier);
            return relativeDirectoryPath + sourceFileName;
        }

        // http://msdn.microsoft.com/en-us/library/ff547478%28v=vs.85%29.aspx
        /// <returns>In the following form: 'WinNT\x86\'</returns>
        public static string GetRelativeDirectoryPath(PNPDriverINFFile pnpDriverInf, string sourceFileName, string architectureIdentifier)
        {
            string subdir;
            string diskID = GeSourceFileDiskID(pnpDriverInf, sourceFileName, architectureIdentifier, out subdir);

            if (diskID == String.Empty)
            {
                // file location can come from either [SourceDisksFiles] section for vendor-provided drivers
                // or from layout.inf for Microsoft-provided drivers.
                // if there is no [SourceDisksFiles] section, we assume the user used a Microsoft-provided driver
                // and put all the necessary files in the root driver directory (where the .inf is located)
                //
                // Note: if [SourceDisksFiles] is not present, Windows GUI-mode setup will look for the files in the root driver directory as well.
                return String.Empty;
            }
            else
            {
                if (subdir.StartsWith(@"\"))
                {
                    subdir = subdir.Substring(1);
                }

                string relativePathToDisk = GeSourceDiskPath(pnpDriverInf, diskID, architectureIdentifier);
                if (relativePathToDisk == null)
                {
                    Console.WriteLine("Warning: Could not locate DiskID '{0}'", diskID);
                    return String.Empty; // Which means that the file is in the driver directory
                }
                else if (relativePathToDisk == String.Empty)
                {
                    // No path, which means that the disk root is the driver directory
                    return subdir + @"\";
                }
                else if (relativePathToDisk.StartsWith(@"\"))
                {
                    // We remove the leading backslash, and return the relative directory name + subdir
                    return relativePathToDisk.Substring(1) + @"\" + subdir + @"\";
                }
                else
                {
                    Console.WriteLine("Warning: Invalid entry for DiskID '{0}'", diskID);
                    return subdir + @"\";
                }
            }
        }

        public static string GetEnumeratorNameFromHardwareID(string hardwareID)
        {
            if (hardwareID.StartsWith("*"))
            {
                return "*";
            }
            else
            {
                int index = hardwareID.IndexOf(@"\");
                string enumerator = hardwareID.Substring(0, index);
                return enumerator;
            }
        }

        // we keep the original function name
        /// <summary>
        /// Calculate hash to be used as the hash component in a Device's ParentIdPrefix (has the form <level>&<hash>&<instance>)
        /// </summary>
        /// <param name="ustr">Should be the device instanceID, in the form of 'ACPI_HAL\PNP0C08\0' or 'ACPI\PNP0A08\1'</param>
        private static ulong HASH_UNICODE_STRING(string ustr)
        {
            ulong _chHolder = 0;
            for (int index = 0; index < ustr.Length; index++)
            {
                char _p = ustr[index];
                _chHolder = (uint)(37 * _chHolder) + (uint)(_p);
            }
            ulong result = (uint)(314159269 * _chHolder) % 1000000007;
            return result;
        }

        // C source code:
        //#define HASH_UNICODE_STRING( _pustr, _phash ) {                         \
        //PWCHAR _p = (_pustr)->Buffer;                                           \
        //PWCHAR _ep = _p + ((_pustr)->Length/sizeof(WCHAR));                     \
        //ULONG _chHolder =0;                                                     \
        //                                                                        \
        //while( _p < _ep ) {                                                     \
        //    _chHolder = 37 * _chHolder + (unsigned int) (*_p++);                \
        //}                                                                       \
        //                                                                        \
        //*(_phash) = abs(314159269 * _chHolder) % 1000000007;                    \
    }
}
