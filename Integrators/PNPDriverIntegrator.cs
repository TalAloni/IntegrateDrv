using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class PNPDriverIntegrator : PNPDriverIntegratorBase
    {
        WindowsInstallation m_installation;
        private bool m_useLocalHardwareConfig;
        private string m_enumExportPath;
        bool m_preconfigure;

        private string m_classInstanceID = String.Empty;

        // used to prevent collisions in text-mode (Key is the old file name, Value is the new file name)
        private KeyValuePairList<string, string> m_oldToNewFileName = new KeyValuePairList<string, string>();
        
        public PNPDriverIntegrator(PNPDriverDirectory driverDirectory, WindowsInstallation installation, string hardwareID, bool useLocalHardwareConfig, string enumExportPath, bool preconfigure)
            : base(driverDirectory, installation.ArchitectureIdentifier, installation.MinorOSVersion, installation.ProductType, hardwareID)
        {
            m_installation = installation;
            m_useLocalHardwareConfig = useLocalHardwareConfig;
            m_enumExportPath = enumExportPath;
            m_preconfigure = preconfigure;
        }

        public void IntegrateDriver()
        {
            PNPDriverINFFile pnpDriverInf;
            string installSectionName = this.DriverDirectory.GetDeviceInstallSectionName(this.HardwareID, m_installation.ArchitectureIdentifier, m_installation.MinorOSVersion, m_installation.ProductType, out pnpDriverInf);
            if (installSectionName == String.Empty)
            {
                Console.WriteLine("Unable to locate InstallSectionName in INF file");
                Program.Exit();
            }

            m_classInstanceID = m_installation.SetupRegistryHive.AllocateClassInstanceID(pnpDriverInf.ClassGUID);

            ProcessInstallSection(pnpDriverInf, installSectionName, m_classInstanceID);
            ProcessInstallServicesSection(pnpDriverInf, installSectionName);
            // this.DeviceServices is now populated

            if (this.DeviceServices.Count == 0)
            {
                Console.WriteLine("Error: driver does not have an associated service, IntegrateDrv will not proceed.");
                Program.Exit();
            }

            PrepareToPreventTextModeDriverNameCollision(this.DeviceServices);

            foreach (DeviceService deviceService in this.DeviceServices)
            {
                InstructToLoadTextModeDeviceService(pnpDriverInf, deviceService);
                RegisterDeviceService(m_installation.SetupRegistryHive, pnpDriverInf, deviceService);
                RegisterDeviceService(m_installation.HiveSystemInf, pnpDriverInf, deviceService);
            }

            CopyDriverFiles(this.DeviceServices);

            // register the device:

            if (PNPDriverINFFile.IsRootDevice(this.HardwareID))
            {
                // installing virtual device: (this is critical for some services such as iScsiPrt)
                string virtualDeviceInstanceID = m_installation.AllocateVirtualDeviceInstanceID(pnpDriverInf.ClassName);
                if (this.DeviceServices.Count > 0)
                {
                    DeviceService deviceService = this.DeviceServices[0];
                    PreconfigureDeviceInstance(pnpDriverInf, "Root", pnpDriverInf.ClassName.ToUpper(), virtualDeviceInstanceID, deviceService);
                }
            }
            else // physical device 
            {
                RegisterPhysicalDevice(pnpDriverInf);

                // GUI-Mode setup will scan all of the directories listed under "DevicePath" directories, 
                // if it will find multiple matches, it will use the .inf file that has the best match.
                // Microsoft does not define exactly how matching drivers are ranked, observations show that:
                // 1. When both .inf have the exact same hardwareID, and one of the .inf is signed and the other is not, the signed .inf will qualify as the best match.
                // 2. When both .inf have the exact same hardwareID, and both of the .inf files are unsigned, the .inf with the most recent version / date will qualify as the best match.
                // 3. When both .inf have the exact same hardwareID, and both of the .inf files are unsigned, and both has the same version / date, the .inf from the first directory listed under "DevicePath" will qualify as the best match.

                // We have to disable the device drivers included in windows to qualify the newly integrated drivers as best match:
                PNPDriverGUIModeIntegrator.DisableInBoxDeviceDrivers(m_installation.SetupDirectory, m_installation.ArchitectureIdentifier, m_installation.MinorOSVersion, m_installation.ProductType, this.HardwareID);
            }

            // Network Device:
            // We want to make the NIC driver accessible to windows GUI mode setup, otherwise no 'Network Connection' will be installed and TCP/IP configuration
            // for the NIC will be deleted. (and as a result, the NIC would not have TCP/IP bound to it)

            // Devices in general:
            // Windows will clear all existing Enum and / or Control\Class entries of devices that have no matching driver available during GUI-mode setup
            // (it will be done near the very end of GUI-mode setup)
            // So we let Windows GUI-Mode install the device.

            // Note: the driver will be modified for boot start
            PNPDriverGUIModeIntegrator guiModeIntegrator = new PNPDriverGUIModeIntegrator(this.DriverDirectory, m_installation, this.HardwareID);
            guiModeIntegrator.Integrate();
        }

        public void RegisterPhysicalDevice(PNPDriverINFFile pnpDriverInf)
        {
            if (m_preconfigure && pnpDriverInf.IsNetworkAdapter && (m_useLocalHardwareConfig || m_enumExportPath != String.Empty))
            {
                string deviceID;
                string deviceInstanceID;

                if (m_useLocalHardwareConfig)
                {
                    deviceInstanceID = PNPLocalHardwareDetector.DetectLocalDeviceInstanceID(this.HardwareID, out deviceID);
                    if (deviceInstanceID == String.Empty)
                    {
                        Console.WriteLine("Warning: Could not detect matching device installed locally, configuration will not be applied!");
                    }
                }
                else // m_enumExportPath != String.Empty
                {
                    deviceInstanceID = PNPExportedHardwareDetector.DetectExportedDeviceInstanceID(m_enumExportPath, this.HardwareID, out deviceID);
                    if (deviceInstanceID == String.Empty)
                    {
                        Console.WriteLine("Warning: Could not detect matching device in the exported registry, configuration will not be applied!");
                    }
                }

                if (deviceInstanceID != String.Empty)
                {
                    // m_netDeviceServices is now populated
                    if (this.NetworkDeviceServices.Count > 0)
                    {
                        // unlike other types of hardware (SCSI controllers etc.), it's not enough to add a NIC to the 
                        // Criticla Device Database (CDDB) to make it usable during boot, as mentioned in the comments above RegisterNicAsCriticalDevice()
                        // at the very least, a CDDB entry and a "Device" registry value under Enum\Enumerator\DeviceID\DeviceInstanceID is required
                        // (as well as DeviceDesc if not automatically added by the kernel-PNP)
                        // here we manually register the hardware in advance, but it's better to use NICBootConf to do this during boot,
                        // NICBootConf will also work if the NIC has been moved to another PCI slot since creating the installation media.

                        // the first item in m_netDeviceServices should be the actual NIC (CHECKME: what about NIC / bus driver combination like nVIdia)
                        NetworkDeviceService deviceService = this.NetworkDeviceServices[0];
                        string enumerator = PNPDriverIntegratorUtils.GetEnumeratorNameFromHardwareID(this.HardwareID);
                        PreconfigureDeviceInstance(pnpDriverInf, enumerator, deviceID, deviceInstanceID, deviceService);
                    }
                    else
                    {
                        Console.WriteLine("Warning: failed to install '{0}', because the service for this network adapter has not been registered!", this.HardwareID);
                    }
                }
            }
            else
            {
                // if it's a NIC, We assume the user will integrate NICBootConf, which will configure the network adapter during boot.
                // we'll just add the device to the Criticla Device Database (CDDB), and let kernel-PNP and NICBootConf do the rest.
                if (pnpDriverInf.IsNetworkAdapter)
                {
                    // NICBootConf needs the ClassGUID in place for each DeviceInstance Key,
                    // if we put the ClassGUID in the CDDB, the ClassGUID will be applied to each DeviceInstance with matching hardwareID
                    m_installation.TextSetupInf.AddDeviceToCriticalDeviceDatabase(this.HardwareID, this.DeviceServices[0].ServiceName, PNPDriverINFFile.NetworkAdapterClassGUID);
                    m_installation.HiveSystemInf.AddDeviceToCriticalDeviceDatabase(this.HardwareID, this.DeviceServices[0].ServiceName, PNPDriverINFFile.NetworkAdapterClassGUID);
                }
                else
                {
                    m_installation.TextSetupInf.AddDeviceToCriticalDeviceDatabase(this.HardwareID, this.DeviceServices[0].ServiceName);
                    m_installation.HiveSystemInf.AddDeviceToCriticalDeviceDatabase(this.HardwareID, this.DeviceServices[0].ServiceName);
                }
            }
        }

        // unlike other types of hardware (SCSI controllers etc.), it's not enough to add a NIC to the 
        // Criticla Device Database (CDDB) to make it usable during boot (Note that NIC driver is an NDIS
        // miniport driver, and the driver does not have an AddDevice() routine and instead uses NDIS' AddDevice())
        // This method performs the additional steps needed for a NIC that is added to the CDDB, which are basically letting Windows
        // know which device class instance is related to the device (TCP/IP settings are tied to the device class instance)
        // The above is true for both text-mode and GUI-mode / Final Windows.
        // Note: it's best to use a driver that does these steps during boot, I have written NICBootConf for that purpose.
        /*
        private void PreconfigureCriticalNetworkAdapter(PNPDriverINFFile pnpDriverInf, string enumerator, string deviceID, string deviceInstanceID, DeviceService deviceService)
        {
            string keyName = @"ControlSet001\Enum\" + enumerator + @"\" + deviceID + @"\" + deviceInstanceID;
            m_installation.SetupRegistryHive.SetRegistryKey(keyName, "Driver", RegistryValueKind.String, pnpDriverInf.ClassGUID + @"\" + m_classInstanceID);
            // The presence of DeviceDesc is critical for some reason, but any value can be used
            m_installation.SetupRegistryHive.SetRegistryKey(keyName, "DeviceDesc", RegistryValueKind.String, deviceService.DeviceDescription);

            // not critical:
            m_installation.SetupRegistryHive.SetRegistryKey(keyName, "ClassGUID", RegistryValueKind.String, pnpDriverInf.ClassGUID);

            // we must not specify ServiceName or otherwise kernel-PNP will skip this device

            // let kernel-PNP take care of the rest for us, ClassGUID is not critical:
            m_installation.TextSetupInf.AddDeviceToCriticalDeviceDatabase(this.HardwareID, deviceService.ServiceName);
        }
        */

        /// <summary>
        /// When using this method, there is no need to use the Critical Device Database
        /// </summary>
        private void PreconfigureDeviceInstance(PNPDriverINFFile pnpDriverInf, string enumerator, string deviceID, string deviceInstanceID, DeviceService deviceService)
        {
            PreconfigureDeviceInstance(pnpDriverInf, m_installation.SetupRegistryHive, enumerator, deviceID, deviceInstanceID, deviceService);
            // Apparently this is not necessary for the devices to work properly in GUI-mode, because configuration will stick from text-mode setup:
            PreconfigureDeviceInstance(pnpDriverInf, m_installation.HiveSystemInf, enumerator, deviceID, deviceInstanceID, deviceService);
        }

        private void PreconfigureDeviceInstance(PNPDriverINFFile pnpDriverInf, ISystemRegistryHive systemRegistryHive, string enumerator, string deviceID, string deviceInstanceID, DeviceService deviceService)
        {
            string driver = pnpDriverInf.ClassGUID.ToUpper() + @"\" + m_classInstanceID;
            string manufacturerName = pnpDriverInf.GetDeviceManufacturerName(this.HardwareID, m_installation.ArchitectureIdentifier, m_installation.MinorOSVersion, m_installation.ProductType);

            string hardwareKeyName = @"Enum\" + enumerator + @"\" + deviceID + @"\" + deviceInstanceID;

            systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "ClassGUID", RegistryValueKind.String, pnpDriverInf.ClassGUID);
            // The presence of DeviceDesc is critical for some reason, but any value can be used
            systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "DeviceDesc", RegistryValueKind.String, deviceService.DeviceDescription);
            // "Driver" is used to help Windows determine which software key belong to this hardware key.
            // Note: When re-installing the driver, the software key to be used will be determined by this value as well.
            systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "Driver", RegistryValueKind.String, driver);
            systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "Service", RegistryValueKind.String, deviceService.ServiceName);

            // ConfigFlags is not related to the hardware, it's the status of the configuration of the device by Windows (CONFIGFLAG_FAILEDINSTALL etc.)
            // the presence of this value tells windows the device has driver installed
            systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "ConfigFlags", RegistryValueKind.DWord, 0);

            if (PNPDriverINFFile.IsRootDevice(this.HardwareID))
            {
                // Windows uses the "HardwareID" entry to determine if the hardware is already installed,
                // We don't have to add this value for physical devices, because Windows will get this value from the device,
                // but we must add this for virtual devices, or we will find ourselves with duplicity when re-installing (e.g. two Microsoft iScsi Initiators).
                systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "HardwareID", RegistryValueKind.MultiString, new string[] { this.HardwareID });
            }

            // not necessary:
            systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "Mfg", RegistryValueKind.String, manufacturerName);
            systemRegistryHive.SetCurrentControlSetRegistryKey(hardwareKeyName, "Class", RegistryValueKind.String, pnpDriverInf.ClassName);
        }

        // An explanation about driver name collision in text-mode:
        // in text-mode, the serviceName will be determined by the name of the file, and AFAIK there is no way around it,
        // so in order for a driver such as the Microsoft-provided Intel E1000 to work properly (service name: E1000, filename: e1000325.sys),
        // we are left with two choices:
        // 1. create the registry entries in the wrong place, e.g. under Services\serviceFileName-without-sys-extension (e.g. Services\e1000325)
        // 2. rename the serviceFileName to match the correct serviceName (e.g. e1000325.sys becomes E1000.sys)
        // (there is also a third option to install the service under both names - but it's a really messy proposition)

        // the first option will work for text-mode, but will break the GUI mode later (CurrentControlSet\Enum entries will be incorrect,
        // and trying to overwrite them with hivesys.inf will not work AFAIK).
        // so we are left with the second option, it's easy to do in the case of the Intel E1000, we will simply use E1000.sys for text-mode,
        // the problem is when there is already a dependency that uses the file name we want to use, 
        // the perfect example is Microsoft iSCSI initiator (service name: iScsiPrt, service filename: msiscsi.sys, dependency: iscsiprt.sys),
        // we want to rename msiscsi.sys to iscsiprt.sys, but there is a collision, because iscsiprt.sys is already taken by a necessary dependency,
        // the solution is to rename iscsiprt.sys to a different name (iscsip_o.sys), and patch(!) msiscsi.sys (which now becomes iscsiprt.sys) to use the new dependency name.

        // this method will test if there is a collision we will need to take care of later, and populate the needed variables.
        /// <param name="serviceFileName">the file name of the service executable</param>
        /// <param name="serviceName">The name of the service subkey under CurrentControlSet\Services</param>
        public void PrepareToPreventTextModeDriverNameCollision(List<DeviceService> deviceServices)
        {
            List<string> serviceFileNames = new List<string>();
            List<string> expectedServiceFileNames = new List<string>();
            foreach (DeviceService deviceService in deviceServices)
            {
                serviceFileNames.Add(deviceService.FileName);
                expectedServiceFileNames.Add(deviceService.TextModeFileName);
            }

            // we put the filenames with a name matching the service executable at the top
            int insertIndex = 0;
            for (int index = 0; index < this.DriverFilesToCopy.Count; index++)
            {
                string fileName = this.DriverFilesToCopy[index].DestinationFileName;
                if (StringUtils.ContainsCaseInsensitive(serviceFileNames, fileName))
                {
                    FileToCopy serviceExecutableEntry = this.DriverFilesToCopy[index];
                    this.DriverFilesToCopy.RemoveAt(index);
                    this.DriverFilesToCopy.Insert(insertIndex, serviceExecutableEntry);
                    insertIndex++;
                }
            }
            // now the service executables are at the top
            for (int index = insertIndex; index < this.DriverFilesToCopy.Count; index++)
            {
                string fileName = this.DriverFilesToCopy[index].DestinationFileName;
                int collisionIndex = StringUtils.IndexOfCaseInsensitive(expectedServiceFileNames, fileName);
                if (collisionIndex >= 0)
                {
                    string serviceName = deviceServices[collisionIndex].ServiceName;
                    string newFileName = serviceName.Substring(0, serviceName.Length - 2) + "_o.sys";
                    m_oldToNewFileName.Add(fileName, newFileName);
                    Console.WriteLine("Using special measures to prevent driver naming collision");
                }
            }
        }

        // see comments above PrepareToPreventTextModeDriverNameCollision() ^^
        /// <summary>
        /// Will copy PNP driver files to setup and boot directories, and update txtsetup.inf accordingly.
        /// The modifications support 3 different installation scenarions: 
        /// 1.  The user install using unmodified CD, use this program to integrate the drivers to the temporary installation folder that was created and then boot from it.
        /// 2.  The user uses this program to create modified installation folder / CD, boots from Windows PE
        ///     at the target machine, and use winnt32.exe to install the target OS. (DOS / winnt.exe should work too)
        /// 3. The user uses this program to create modified installation CD and boot from it.
        /// Note: We do not support RIS (seems too complex and can collide with our own TCP/IP integration)
        /// </summary>
        public void CopyDriverFiles(List<DeviceService> deviceServices)
        {
            List<string> serviceFileNames = new List<string>();
            foreach (DeviceService deviceService in deviceServices)
            {
                serviceFileNames.Add(deviceService.FileName);
            }

            for (int index = 0; index < this.DriverFilesToCopy.Count; index++)
            {
                string sourceFilePath = this.DriverDirectory.Path + this.DriverFilesToCopy[index].RelativeSourceFilePath;
                string fileName = this.DriverFilesToCopy[index].DestinationFileName;
                bool serviceWithNameCollision = false;

                string textModeFileName;
                if (fileName.ToLower().EndsWith(".sys"))
                {
                    int serviceIndex = StringUtils.IndexOfCaseInsensitive(serviceFileNames, fileName);
                    if (serviceIndex >= 0)
                    {
                        string serviceName = deviceServices[index].ServiceName;
                        textModeFileName = deviceServices[index].TextModeFileName;
                        serviceWithNameCollision = StringUtils.ContainsCaseInsensitive(m_oldToNewFileName.Keys, textModeFileName);
                        
                        if (serviceName.Length > 8 && !m_installation.IsTargetContainsTemporaryInstallation)
                        {
                            Console.WriteLine("Warning: Service '{0}' has name longer than 8 characters.", serviceName);
                            Console.Write("********************************************************************************");
                            Console.Write("*You must use ISO level 2 compatible settings if you wish to create a working  *");
                            Console.Write("*bootable installation CD.                                                     *");
                            Console.Write("*if you're using nLite, choose mkisofs over the default ISO creation engine.   *");
                            Console.Write("********************************************************************************");
                        }
                    }
                    else
                    {
                        int renameIndex = StringUtils.IndexOfCaseInsensitive(m_oldToNewFileName.Keys, fileName);
                        if (renameIndex >= 0)
                        {
                            textModeFileName = m_oldToNewFileName[renameIndex].Value;
                        }
                        else
                        {
                            textModeFileName = fileName;
                        }
                    }
                }
                else
                {
                    textModeFileName = fileName;
                }

                if (fileName.ToLower().EndsWith(".sys") || fileName.ToLower().EndsWith(".dll"))
                {
                    // we copy all the  executables to the setup directory, Note that we are using textModeFileName
                    // (e.g. e1000325.sys becomes E1000.sys) this is necessary for a bootable cd to work properly)
                    // but we have to rename the file during text-mode copy phase for GUI-mode to work properly
                    ProgramUtils.CopyCriticalFile(sourceFilePath, m_installation.SetupDirectory + textModeFileName, true);
                }

                // see comments above PrepareToPreventTextModeDriverNameCollision() ^^
                // in case of a service name collision, here we patch the service executable file that we just copied and update the name of its dependency
                if (serviceWithNameCollision)
                {
                    // we need the renamed patched file in the setup (e.g. I386 folder) for a bootable cd to work properly
                    PreventTextModeDriverNameCollision(m_installation.SetupDirectory + textModeFileName);

                    // we need the original file too (for GUI-mode)
                    ProgramUtils.CopyCriticalFile(sourceFilePath, m_installation.SetupDirectory + fileName);
                }

                // update txtsetup.sif:
                if (fileName.ToLower().EndsWith(".sys"))
                {
                    // this is for the GUI-mode, note that we copy the files to their destination using their original name,
                    // also note that if there is a collision we copy the original (unpatched) file instead of the patched one.
                    if (serviceWithNameCollision)
                    {
                        // this is the unpatched file:
                        m_installation.TextSetupInf.SetSourceDisksFileDriverEntry(m_installation.ArchitectureIdentifier, fileName, FileCopyDisposition.AlwaysCopy, fileName);

                        // this is the patched file, we're not copying it anywhere, but we load this service executable so text-mode setup demand an entry (probably to locate the file source directory)
                        m_installation.TextSetupInf.SetSourceDisksFileDriverEntry(m_installation.ArchitectureIdentifier, textModeFileName, FileCopyDisposition.DoNotCopy);
                    }
                    else
                    {
                        m_installation.TextSetupInf.SetSourceDisksFileDriverEntry(m_installation.ArchitectureIdentifier, textModeFileName, FileCopyDisposition.AlwaysCopy, fileName);
                    }
                }
                else if (fileName.ToLower().EndsWith(".dll"))
                {
                    m_installation.TextSetupInf.SetSourceDisksFileDllEntry(m_installation.ArchitectureIdentifier, fileName);
                }
                // finished updating txtsetup.sif

                if (m_installation.IsTargetContainsTemporaryInstallation)
                {
                    if (fileName.ToLower().EndsWith(".sys"))
                    {
                        // we copy all drivers by their text-mode name
                        ProgramUtils.CopyCriticalFile(m_installation.SetupDirectory + textModeFileName, m_installation.BootDirectory + textModeFileName);
                    }
                }
                else
                {
                    // update dosnet.inf
                    if (fileName.ToLower().EndsWith(".sys"))
                    {
                        // we already made sure all the files in the setup directory are using their textModeFileName
                        m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToLocalSourceRootDirectory(textModeFileName, textModeFileName);
                        m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory(textModeFileName, textModeFileName);

                        if (serviceWithNameCollision)
                        {
                            // the unpatched .sys should be available with it's original (GUI) name in the \$WINNT$.~LS folder
                            m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToLocalSourceRootDirectory(fileName);
                        }
                    }
                    else if (fileName.ToLower().EndsWith(".dll"))
                    {
                        // in the case of .dll fileName is the same as textModeFileName
                        m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToLocalSourceRootDirectory(fileName);
                    }
                }
            }
        }

        private void PreventTextModeDriverNameCollision(string filePath)
        {
            bool serviceNameCollisionDetected = (m_oldToNewFileName.Count > 0);
            if (serviceNameCollisionDetected)
            {
                List<string> dependencies = PortableExecutableUtils.GetDependencies(filePath);
                for(int index = 0; index < m_oldToNewFileName.Count; index++)
                {
                    string oldFileName = m_oldToNewFileName[index].Key;
                    string newFileName = m_oldToNewFileName[index].Value;
                    if (dependencies.Contains(oldFileName)) // this happens for iscsiprt / msiscsi
                    {
                        PortableExecutableUtils.RenameDependencyFileName(filePath, oldFileName, newFileName);
                    }
                }
            }
        }

        public override void SetCurrentControlSetRegistryKey(string keyName, string valueName, RegistryValueKind valueKind, object valueData)
        {
            // text-mode
            m_installation.SetupRegistryHive.SetCurrentControlSetRegistryKey(keyName, valueName, valueKind, valueData);
            // GUI-mode
            m_installation.HiveSystemInf.SetCurrentControlSetRegistryKey(keyName, valueName, valueKind, valueData);
        }

        private void InstructToLoadTextModeDeviceService(PNPDriverINFFile pnpDriverInf, DeviceService deviceService)
        {
            // update txtsetup.sif
            if (deviceService.ServiceGroup == String.Empty)
            {
                // No group, which means txtsetup.sif will have effect on initialization order.
                // In final Windows this means the service is initialized after all other services.
                // To do the same in text-mode, we should load this service last (which means using the [CdRomDrivers.Load] section):
                m_installation.TextSetupInf.InstructToLoadCdRomDriversDriver(deviceService.TextModeFileName, deviceService.DeviceDescription);
            }
            else
            {
                // we have set a group in setupreg.hiv, so for text-mode it doesn't matter where we put the service in txtsetup.sif,
                // however, some of the [xxxx.Load] groups will stick and cause problems later (GUI-mode / final Windows),
                // see TextSetupINFFile.Load.cs to see which groups may cause problems
                //
                // Note that the service is renamed back to its original name if necessary.
                m_installation.TextSetupInf.InstructToLoadKeyboardDriver(deviceService.TextModeFileName, deviceService.DeviceDescription);
            }
        }

        private void RegisterDeviceService(ISystemRegistryHive systemRegistryHive, PNPDriverINFFile pnpDriverInf, DeviceService deviceService)
        {
            // We ignore start type. if the user uses this program, she wants to boot something!
            int startType = 0;
            // Note: using a different service registry key under CurrentControlSet\Services with an ImagePath entry referring to the .sys will not work in text mode setup!
            // Text-mode setup will always initialize services based on the values stored under Services\serviceName, where serviceName is the service file name without the .sys extension.

            // write all to registry:
            string serviceName = deviceService.ServiceName;
            if (deviceService.ServiceDisplayName != String.Empty)
            {
                systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "DisplayName", RegistryValueKind.String, deviceService.ServiceDisplayName);
            }
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "ErrorControl", RegistryValueKind.DWord, deviceService.ErrorControl);
            if (deviceService.ServiceGroup != String.Empty)
            {
                systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "Group", RegistryValueKind.String, deviceService.ServiceGroup);
            }
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "Start", RegistryValueKind.DWord, startType);
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "Type", RegistryValueKind.DWord, deviceService.ServiceType);

            if (systemRegistryHive is HiveSystemINFFile) // GUI Mode registry
            {
                systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "ImagePath", RegistryValueKind.String, deviceService.ImagePath);
            }

            // Note that software key will stick from text-mode:
            string softwareKeyName = @"Control\Class\" + pnpDriverInf.ClassGUID + @"\" + m_classInstanceID;

            if (deviceService is NetworkDeviceService)
            {
                string netCfgInstanceID = ((NetworkDeviceService)deviceService).NetCfgInstanceID;
                // - sanbootconf and iScsiBP use this value, but it's not necessary for successful boot, static IP can be used instead.
                // - the presence of this value will stick and stay for the GUI mode
                // - the presence of this value during GUI Mode will prevent the IP settings from being resetted
                // - the presence of this value will cause Windows 2000 \ XP x86 to hang after the NIC driver installation (there is no problem with Windows Server 2003)
                // - the presence of this value will cause Windows XP x64 to hang during the "Installing Network" phase (there is no problem with Windows Server 2003)

                // we will set this value so sanbootconf / iScsiBP could use it, and if necessary, delete it before the NIC driver installation (using hal.inf)
                systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "NetCfgInstanceId", RegistryValueKind.String, netCfgInstanceID);
                if (!m_installation.IsWindowsServer2003)
                {
                    // delete the NetCfgInstanceId registry value during the beginning of GUI-mode setup
                    m_installation.HalInf.DeleteNetCfgInstanceIdFromNetworkAdapterClassInstance(m_classInstanceID);
                }

                // The Linkage subkey is critical, and is used to bind the network adapter to TCP/IP:
                // - The NetCfgInstanceId here is the one Windows actually uses for TCP/IP configuration.
                // - The first component in one entry corresponds to the first component in the other entries:
                systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "Linkage", "Export", RegistryValueKind.MultiString, new string[] { @"\Device\" + netCfgInstanceID });
                systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "Linkage", "RootDevice", RegistryValueKind.MultiString, new string[] { netCfgInstanceID }); // Windows can still provide TCP/IP without this entry
                systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "Linkage", "UpperBind", RegistryValueKind.MultiString, new string[] { "Tcpip" });
            }

            // We need to make sure the software key is created, otherwise two devices can end up using the same software key

            // Note for network adapters:
            // "MatchingDeviceId" is not critical for successfull boot or devices which are not network adapters, but it's critical for NICBootConf in case it's being used
            // Note: Windows will store the hardwareID as it appears in the driver, including &REV
            systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "MatchingDeviceId", RegistryValueKind.String, this.HardwareID.ToLower());

            // not necessary. in addition, it will also be performed by GUI-mode setup
            if (deviceService.DeviceDescription != String.Empty)
            {
                systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "DriverDesc", RegistryValueKind.String, deviceService.DeviceDescription);
            }
            if (pnpDriverInf.DriverVersion != String.Empty)
            {
                systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "DriverVersion", RegistryValueKind.String, pnpDriverInf.DriverVersion);
            }
            if (pnpDriverInf.Provider != String.Empty)
            {
                systemRegistryHive.SetCurrentControlSetRegistryKey(softwareKeyName, "ProviderName", RegistryValueKind.String, pnpDriverInf.Provider);
            }
        }
    }
}
