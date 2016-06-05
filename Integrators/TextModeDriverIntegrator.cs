using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class TextModeDriverIntegrator
    {
        TextModeDriverDirectory m_driverDirectory;
        WindowsInstallation m_installation;
        string m_deviceID = String.Empty;

        public TextModeDriverIntegrator(TextModeDriverDirectory driverDirectory, WindowsInstallation installation, string deviceID)
        {
            m_driverDirectory = driverDirectory;
            m_installation = installation;
            m_deviceID = deviceID;
        }

        public static string UISelectDeviceID(TextModeDriverDirectory driverDirectory)
        {
            TextModeDriverSetupINIFile driverINI = driverDirectory.TextModeDriverSetupINI;

            string deviceID = String.Empty;

            if (driverINI.Devices.Count > 1)
            {
                Console.WriteLine("The directory specified contains drivers for the following devices:");
                for (int index = 0; index < driverINI.Devices.Count; index++)
                {
                    Console.WriteLine("{0}. {1}", index + 1, driverINI.Devices[index].Value);
                }
                Console.Write("Select the device driver you wish to integrate: ");
                // driver number could be double-digit, so we use ReadLine()
                int selection = Conversion.ToInt32(Console.ReadLine()) - 1;
                if (selection >= 0 && selection < driverINI.Devices.Count)
                {
                    deviceID = driverINI.Devices[selection].Key;
                    return deviceID;
                }
                else
                {
                    Console.WriteLine("Error: No driver has been selected, exiting.");
                    return String.Empty;
                }
            }
            else if (driverINI.Devices.Count == 1)
            {
                deviceID = driverINI.Devices[0].Key;
                string deviceName = driverINI.Devices[0].Value;
                Console.WriteLine("Found one driver:");
                Console.WriteLine("1. " + deviceName);
                return deviceID;
            }
            else
            {
                Console.WriteLine("Mass storage device driver has not been found.");
                return String.Empty;
            }
        }

        public void IntegrateDriver()
        {
            UpdateTextSetupInformationFileAndCopyFiles(m_deviceID);
            string destinationWinntDirectory = m_installation.GetDriverDestinationWinntDirectory(m_deviceID);

            // hivesft.inf
            m_installation.HiveSoftwareInf.RegisterDriverDirectory(destinationWinntDirectory);

            if (m_installation.Is64Bit)
            {
                // hivsft32.inf
                m_installation.HiveSoftware32Inf.RegisterDriverDirectory(destinationWinntDirectory);
            }
        }

        // update txtsetup.sif and dotnet.inf
        private void UpdateTextSetupInformationFileAndCopyFiles(string deviceID)
        {
            // Files.HwComponent.ID Section
            TextModeDriverSetupINIFile driverINI = m_driverDirectory.TextModeDriverSetupINI;
            List<string> section = driverINI.GetDriverFilesSection(deviceID);

            string serviceName = String.Empty;
            List<string> driverKeys = new List<string>();

            string sourceDirectoryInMediaRootForm = m_installation.GetSourceDriverDirectoryInMediaRootForm(deviceID);
            int sourceDiskID = m_installation.TextSetupInf.AllocateSourceDiskID(m_installation.ArchitectureIdentifier, sourceDirectoryInMediaRootForm);
            
            string destinationWinntDirectory = m_installation.GetDriverDestinationWinntDirectory(m_deviceID);
            int destinationWinntDirectoryID = m_installation.TextSetupInf.AllocateWinntDirectoryID(destinationWinntDirectory);

            foreach (string line in section)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                string fileType = keyAndValues.Key;
                string directory = driverINI.GetDirectoryOfDisk(keyAndValues.Value[0]);
                string fileName = keyAndValues.Value[1];
                string sourceFilePath = m_driverDirectory.Path + "." + directory + @"\" + fileName;
                bool isDriver = keyAndValues.Key.Equals("driver", StringComparison.InvariantCultureIgnoreCase);
                m_installation.CopyFileToSetupDriverDirectory(sourceFilePath, deviceID + @"\", fileName);
                
                if (isDriver)
                {
                    m_installation.CopyDriverToSetupRootDirectory(sourceFilePath, fileName);
                    if (m_installation.IsTargetContainsTemporaryInstallation)
                    {
                        m_installation.CopyFileFromSetupDirectoryToBootDirectory(fileName);
                    }
                }
                
                m_installation.TextSetupInf.SetSourceDisksFileEntry(m_installation.ArchitectureIdentifier, sourceDiskID, destinationWinntDirectoryID, fileName, FileCopyDisposition.AlwaysCopy);

                if (isDriver)
                {
                    // http://msdn.microsoft.com/en-us/library/ff544919%28v=VS.85%29.aspx
                    // unlike what one may understand from the reading specs, this value is *only* used to form [Config.DriverKey] section name,
                    // and definitely NOT to determine the service subkey name under CurrentControlSet\Services. (which is determined by the service file name without a .sys extension)
                    string driverKey = keyAndValues.Value[2];

                    // http://support.microsoft.com/kb/885756
                    // according to this, only the first driver entry should be processed.

                    // http://app.nidc.kr/dirver/IBM_ServerGuide_v7.4.17/sguide/w3x64drv/$oem$/$1/drv/dds/txtsetup.oem
                    // however, this sample and my experience suggest that files / registry entries from a second driver entry will be copied / registered,
                    // (both under the same Services\serviceName key), so we'll immitate that.
                    driverKeys.Add(driverKey);
                    
                    if (serviceName == String.Empty)
                    {
                        // Some txtsetup.oem drivers are without HardwareID entries,
                        // but we already know that the service is specified by the file name of its executable image without a .sys extension,
                        // so we should use that.
                        serviceName = TextSetupINFFile.GetServiceName(fileName);
                    }
                    // We should use FileCopyDisposition.DoNotCopy, because InstructToLoadSCSIDriver will already copy the device driver.
                    m_installation.TextSetupInf.SetSourceDisksFileDriverEntry(m_installation.ArchitectureIdentifier, fileName, FileCopyDisposition.DoNotCopy);
                    m_installation.TextSetupInf.SetFileFlagsEntryForDriver(fileName);
                    string deviceName = driverINI.GetDeviceName(deviceID);
                    m_installation.TextSetupInf.InstructToLoadSCSIDriver(fileName, deviceName);
                }

                // add file to the list of files to be copied to local source directory
                if (!m_installation.IsTargetContainsTemporaryInstallation)
                {
                    m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToLocalSourceDriverDirectory(sourceDirectoryInMediaRootForm, fileName);
                    if (isDriver)
                    {
                        m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory(fileName);
                    }
                }
            }

            section = driverINI.GetHardwareIdsSection(deviceID);
            foreach (string line in section)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                string hardwareID = keyAndValues.Value[0];
                // http://msdn.microsoft.com/en-us/library/ff546129%28v=VS.85%29.aspx
                // The service is specified by the file name of its executable image without a .sys extension
                // it is incomprehensible that this line will change the value of serviceName, because we already set serviceName to the service file name without a .sys extension
                serviceName = INIFile.Unquote(keyAndValues.Value[1]); 
                hardwareID = INIFile.Unquote(hardwareID);
                m_installation.TextSetupInf.AddDeviceToCriticalDeviceDatabase(hardwareID, serviceName);
            }

            foreach(string driverKey in driverKeys)
            {
                section = driverINI.GetConfigSection(driverKey);
                foreach (string line in section)
                {
                    KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                    string subKeyNameQuoted = keyAndValues.Value[0];
                    string valueName = keyAndValues.Value[1];
                    string valueType = keyAndValues.Value[2];
                    string valueDataUnparsed = keyAndValues.Value[3];
                    RegistryValueKind valueKind = TextModeDriverSetupINIFile.GetRegistryValueKind(valueType);
                    object valueData = HiveINIFile.ParseValueDataString(valueDataUnparsed, valueKind);
                    string subKeyName = INIFile.Unquote(subKeyNameQuoted);

                    m_installation.HiveSystemInf.SetServiceRegistryKey(serviceName, subKeyName, valueName, valueKind, valueData);
                    m_installation.SetupRegistryHive.SetServiceRegistryKey(serviceName, subKeyName, valueName, valueKind, valueData);
                }
            }
        }

        public static void IntegrateTextModeDrivers(List<TextModeDriverDirectory> textModeDriverDirectories, WindowsInstallation installation)
        {
            foreach (TextModeDriverDirectory textModeDriverDirectory in textModeDriverDirectories)
            {
                string deviceID = TextModeDriverIntegrator.UISelectDeviceID(textModeDriverDirectory);
                if (deviceID == String.Empty)
                {
                    // No device has been selected, exit.
                    // UISelectDeviceID has already printed an error message
                    Program.Exit();
                }
                TextModeDriverIntegrator integrator = new TextModeDriverIntegrator(textModeDriverDirectory, installation, deviceID);
                integrator.IntegrateDriver();
            }
        }
    }
}
