using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public abstract class PNPDriverIntegratorBase
    {
        PNPDriverDirectory m_driverDirectory;
        private string m_architectureIdentifier;
        private int m_minorOSVersion;
        private int m_productType;
        private string m_hardwareID = String.Empty; // enumerator-specific-device-id, e.g. PCI\VEN_8086&DEV_100F

        //private string m_classInstanceID = String.Empty;

        private List<DeviceService> m_deviceServices = new List<DeviceService>();
        private List<FileToCopy> m_driverFilesToCopy = new List<FileToCopy>();

        public PNPDriverIntegratorBase(PNPDriverDirectory driverDirectory, string architectureIdentifier, int minorOSVersion, int productType, string hardwareID)
        {
            m_driverDirectory = driverDirectory;
            m_architectureIdentifier = architectureIdentifier;
            m_minorOSVersion = minorOSVersion;
            m_productType = productType;
            m_hardwareID = hardwareID;
        }

        // DDInstall Section in a Network INF File:
        // http://msdn.microsoft.com/en-us/library/ff546329%28VS.85%29.aspx
        protected void ProcessInstallSection(PNPDriverINFFile pnpDriverInf, string installSectionName, string classInstanceID)
        {
            List<string> installSection = pnpDriverInf.GetInstallSection(installSectionName, m_architectureIdentifier, m_minorOSVersion);

            string softwareKeyName = @"Control\Class\" + pnpDriverInf.ClassGUID + @"\" + classInstanceID;
            foreach (string line in installSection)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                switch (keyAndValues.Key)
                {
                    case "AddReg":
                        foreach (string registrySectionName in keyAndValues.Value)
                        {
                            ProcessAddRegSection(pnpDriverInf, registrySectionName, softwareKeyName);
                        }
                        break;
                    case "CopyFiles":
                        if (keyAndValues.Value[0].StartsWith("@"))
                        {
                            ProcessCopyFileDirective(pnpDriverInf, keyAndValues.Value[0].Substring(1));
                        }
                        else
                        {
                            foreach (string copyFilesSectionName in keyAndValues.Value)
                            {
                                ProcessCopyFilesSection(pnpDriverInf, copyFilesSectionName);
                            }
                        }
                        break;
                    case "BusType":
                        if (pnpDriverInf.IsNetworkAdapter)
                        {
                            // Some NICs (AMD PCNet) won't start if BusType is not set (CM_PROB_FAILED_START)
                            int busType = Convert.ToInt32(keyAndValues.Value[0]);

                            SetCurrentControlSetRegistryKey(softwareKeyName, "BusType", RegistryValueKind.String, busType.ToString());
                        }
                        break;
                    case "Characteristics":
                        if (pnpDriverInf.IsNetworkAdapter)
                        {
                            // No evidence so far that the presence of this value is critical, but it's a good practice to add it
                            int characteristics = PNPDriverINFFile.ConvertFromIntStringOrHexString(keyAndValues.Value[0]);

                            SetCurrentControlSetRegistryKey(softwareKeyName, "Characteristics", RegistryValueKind.DWord, characteristics);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        protected void ProcessInstallServicesSection(PNPDriverINFFile pnpDriverInf, string installSectionName)
        {
            List<string> installServicesSection = pnpDriverInf.GetInstallServicesSection(installSectionName, m_architectureIdentifier, m_minorOSVersion);

            foreach (string line in installServicesSection)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                switch (keyAndValues.Key)
                {
                    case "AddService":
                        string serviceName = keyAndValues.Value[0];
                        string serviceInstallSection = keyAndValues.Value[2];
                        string eventLogInstallSection = INIFile.TryGetValue(keyAndValues.Value, 3);
                        string eventLogType = INIFile.TryGetValue(keyAndValues.Value, 4);
                        string eventName = INIFile.TryGetValue(keyAndValues.Value, 5);
                        ProcessServiceInstallSection(pnpDriverInf, serviceInstallSection, serviceName);
                        if (eventLogInstallSection != String.Empty)
                        {
                            // http://msdn.microsoft.com/en-us/library/ff546326%28v=vs.85%29.aspx
                            if (eventLogType == String.Empty)
                            {
                                eventLogType = "System";
                            }
                            if (eventName == String.Empty)
                            {
                                eventName = serviceName;
                            }
                            ProcessEventLogInstallSection(pnpDriverInf, eventLogInstallSection, eventLogType, eventName);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void ProcessEventLogInstallSection(PNPDriverINFFile pnpDriverInf, string sectionName, string eventLogType, string eventName)
        {
            List<string> installSection = pnpDriverInf.GetSection(sectionName);

            string relativeRoot = @"Services\EventLog\" + eventLogType + @"\" + eventName;
            foreach (string line in installSection)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                switch (keyAndValues.Key)
                {
                    case "AddReg":
                        foreach (string registrySectionName in keyAndValues.Value)
                        {
                            ProcessAddRegSection(pnpDriverInf, registrySectionName, relativeRoot);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <param name="relativeRoot">
        /// The location where HKR entried will be stored, relative to 'SYSTEM\CurrentControlSet\' (or ControlSet001 for that matter)
        /// </param>
        private void ProcessAddRegSection(PNPDriverINFFile pnpDriverInf, string sectionName, string relativeRoot)
        {
            List<string> section = pnpDriverInf.GetSection(sectionName);
            foreach (string line in section)
            {
                List<string> values = INIFile.GetCommaSeparatedValues(line);
                string hiveName = values[0];
                string subKeyName = INIFile.Unquote(values[1]);
                string valueName = INIFile.TryGetValue(values, 2);
                string valueType = INIFile.TryGetValue(values, 3); ;
                string valueDataUnparsed = String.Empty;
                if (values.Count > 3)
                {
                    valueDataUnparsed = StringUtils.Join(values.GetRange(4, values.Count - 4), ","); // byte-list is separated using commmas
                }

                valueName = INIFile.Unquote(valueName);
                valueType = pnpDriverInf.ExpandToken(valueType);
                int valueTypeFlags = PNPDriverINFFile.ConvertFromIntStringOrHexString(valueType);
                string valueTypeHexString;
                if (!valueType.StartsWith("0x"))
                {
                    valueTypeHexString = "0x" + valueTypeFlags.ToString("X8"); // we want value type in 8 digit hex string.
                }
                else
                {
                    valueTypeHexString = valueType;
                }
                RegistryValueKind valueKind = PNPDriverINFFile.GetRegistryValueKind(valueTypeFlags);
                if (valueKind == RegistryValueKind.String)
                {
                    valueDataUnparsed = pnpDriverInf.ExpandToken(valueDataUnparsed);
                }
                object valueData = HiveINIFile.ParseValueDataString(valueDataUnparsed, valueKind);

                if (hiveName == "HKR")
                {
                    string cssKeyName = relativeRoot;
                    if (subKeyName != String.Empty)
                    {
                        cssKeyName = cssKeyName + @"\" + subKeyName;
                    }
                    // Note that software key will stick from text-mode:
                    SetCurrentControlSetRegistryKey(cssKeyName, valueName, valueKind, valueData);
                }
                else if (hiveName == "HKLM" && subKeyName.StartsWith(@"SYSTEM\CurrentControlSet\", StringComparison.InvariantCultureIgnoreCase))
                {
                    string cssKeyName = subKeyName.Substring(@"SYSTEM\CurrentControlSet\".Length);

                    SetCurrentControlSetRegistryKey(cssKeyName, valueName, valueKind, valueData);
                }
                else
                {
                    //Console.WriteLine("Warning: unsupported registry path: " + hiveName + @"\" + subKeyName);
                }
            }
        }

        private void ProcessCopyFilesSection(PNPDriverINFFile pnpDriverInf, string sectionName)
        {
            List<string> section = pnpDriverInf.GetSection(sectionName);
            foreach (string line in section)
            {
                List<string> values = INIFile.GetCommaSeparatedValues(line);
                string destinationFileName = values[0];
                string sourceFileName = INIFile.TryGetValue(values, 1);
                if (sourceFileName == String.Empty)
                {
                    sourceFileName = destinationFileName;
                }
                ProcessCopyFileDirective(pnpDriverInf, sourceFileName, destinationFileName);
            }
        }

        private void ProcessCopyFileDirective(PNPDriverINFFile pnpDriverInf, string sourceFileName)
        {
            ProcessCopyFileDirective(pnpDriverInf, sourceFileName, sourceFileName);
        }

        private void ProcessCopyFileDirective(PNPDriverINFFile pnpDriverInf, string sourceFileName, string destinationFileName)
        {
            string relativeSourcePath = PNPDriverIntegratorUtils.GetRelativeDirectoryPath(pnpDriverInf, sourceFileName, m_architectureIdentifier);
            FileToCopy fileToCopy = new FileToCopy(relativeSourcePath, sourceFileName, destinationFileName);
            if (!FileSystemUtils.IsFileExist(m_driverDirectory.Path + fileToCopy.RelativeSourceFilePath))
            {
                Console.WriteLine("Error: Missing file: " + m_driverDirectory.Path + fileToCopy.RelativeSourceFilePath);
                Program.Exit();
            }
            // actual copy will be performed later
            m_driverFilesToCopy.Add(fileToCopy);
        }

        private void ProcessServiceInstallSection(PNPDriverINFFile pnpDriverInf, string sectionName, string serviceName)
        {
            Console.WriteLine("Registering service '" + serviceName + "'");
            List<string> serviceInstallSection = pnpDriverInf.GetSection(sectionName);

            string displayName = String.Empty;
            string serviceBinary = String.Empty;
            string serviceTypeString = String.Empty;
            string errorControlString = String.Empty;
            string loadOrderGroup = String.Empty;

            //string guiModeRelativeRoot = @"Services\" + serviceName;
            foreach (string line in serviceInstallSection)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                switch (keyAndValues.Key)
                {
                    case "AddReg":
                        // http://msdn.microsoft.com/en-us/library/ff546326%28v=vs.85%29.aspx
                        // AddReg will always come after ServiceBinaryServiceBinary

                        string relativeRoot = @"Services\" + serviceName;

                        foreach (string registrySectionName in keyAndValues.Value)
                        {
                            ProcessAddRegSection(pnpDriverInf, registrySectionName, relativeRoot);
                        }
                        break;
                    case "DisplayName":
                        displayName = INIFile.TryGetValue(keyAndValues.Value, 0);
                        break;
                    case "ServiceBinary":
                        serviceBinary = INIFile.TryGetValue(keyAndValues.Value, 0);
                        break;
                    case "ServiceType":
                        serviceTypeString = INIFile.TryGetValue(keyAndValues.Value, 0);
                        break;
                    case "ErrorControl":
                        errorControlString = INIFile.TryGetValue(keyAndValues.Value, 0);
                        break;
                    case "LoadOrderGroup":
                        loadOrderGroup = INIFile.TryGetValue(keyAndValues.Value, 0);
                        break;
                    default:
                        break;
                }
            }

            displayName = pnpDriverInf.ExpandToken(displayName);
            displayName = INIFile.Unquote(displayName);

            string fileName = serviceBinary.Replace(@"%12%\", String.Empty);
            string imagePath = pnpDriverInf.ExpandDirID(serviceBinary);

            int serviceType = PNPDriverINFFile.ConvertFromIntStringOrHexString(serviceTypeString);
            int errorControl = PNPDriverINFFile.ConvertFromIntStringOrHexString(errorControlString);

            string deviceDescription = pnpDriverInf.GetDeviceDescription(m_hardwareID, m_architectureIdentifier, m_minorOSVersion, m_productType);

            DeviceService deviceService;

            if (pnpDriverInf.IsNetworkAdapter)
            {
                // this is a nic, we are binding TCP/IP to it
                // we need a unique NetCfgInstanceID that will be used with Tcpip service and the nic's class
                string netCfgInstanceID = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                deviceService = new NetworkDeviceService(deviceDescription, serviceName, displayName, loadOrderGroup, serviceType, errorControl, fileName, imagePath, netCfgInstanceID);
                m_deviceServices.Add(deviceService);
            }
            else
            {
                deviceService = new DeviceService(deviceDescription, serviceName, displayName, loadOrderGroup, serviceType, errorControl, fileName, imagePath);
                m_deviceServices.Add(deviceService);
            }
        }

        protected void ProcessCoInstallersSection(PNPDriverINFFile pnpDriverInf, string installSectionName)
        {
            string matchingInstallSectionName = pnpDriverInf.GetMatchingInstallSectionName(installSectionName, m_architectureIdentifier, m_minorOSVersion);
            if (matchingInstallSectionName == String.Empty)
            {
                return;
            }
            string matchingCoInstallersSectionName = matchingInstallSectionName + ".CoInstallers";
            List<string> coinstallersSection = pnpDriverInf.GetSection(matchingCoInstallersSectionName);

            foreach (string line in coinstallersSection)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                switch (keyAndValues.Key)
                {
                    case "CopyFiles":
                        if (keyAndValues.Value[0].StartsWith("@"))
                        {
                            ProcessCopyFileDirective(pnpDriverInf, keyAndValues.Value[0].Substring(1));
                        }
                        else
                        {
                            foreach (string copyFilesSectionName in keyAndValues.Value)
                            {
                                ProcessCopyFilesSection(pnpDriverInf, copyFilesSectionName);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public abstract void SetCurrentControlSetRegistryKey(string keyName, string valueName, RegistryValueKind valueKind, object valueData);

        public string HardwareID
        {
            get
            {
                return m_hardwareID;
            }
        }

        public List<DeviceService> DeviceServices
        {
            get
            {
                return m_deviceServices;
            }
        }

        public List<NetworkDeviceService> NetworkDeviceServices
        {
            get
            {
                return DeviceServiceUtils.FilterNetworkDeviceServices(m_deviceServices);
            }
        }

        public PNPDriverDirectory DriverDirectory
        {
            get
            {
                return m_driverDirectory;
            }
        }

        public List<FileToCopy> DriverFilesToCopy
        {
            get
            {
                return m_driverFilesToCopy;
            }
        }
    }

    public class FileToCopy
    {
        public string RelativeSourceDirectory;
        public string SourceFileName;
        public string DestinationFileName;

        public FileToCopy(string relativeSourceDirectory, string sourceFileName, string destinationFileName)
        {
            RelativeSourceDirectory = relativeSourceDirectory;
            SourceFileName = sourceFileName;
            DestinationFileName = destinationFileName;
        }

        public string RelativeSourceFilePath
        {
            get
            {
                return this.RelativeSourceDirectory + this.SourceFileName;
            }
        }
    }
}
