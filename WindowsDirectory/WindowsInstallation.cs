using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class WindowsInstallation
    {
        private const string ScsiDriversSourceDirectory = "SCSIDRV\\"; // this is where the files will reside in the local source directory
        private const string ScsiDriversDestinationDirectory = "TEMP\\SCSIDRV\\"; // this is where the files will reside during windows GUI setup phase

        private string m_targetDirectory = String.Empty;
        private string m_bootDirectory = String.Empty;
        private string m_localSourceDirectory = String.Empty;

        TextSetupINFFile m_textSetupInf;
        HiveSoftwareINFFile m_hiveSoftwareInf;
        HiveSoftware32INFFile m_hiveSoftware32Inf;
        HiveSystemINFFile m_hiveSystemInf;
        DosNetINFFile m_dosNetInf;
        SetupRegistryHiveFile m_setupRegistryHive;
        NetGPCINFFile m_netGPCInf;
        NetPacketSchedulerINFFile m_netPacketSchedulerInf;
        NetPacketSchedulerAdapterINFFile m_netPacketSchedulerAdapterInf;
        NetTCPIPINFFile m_netTCPIP;
        HalINFFile m_halInf;
        UsbINFFile m_usbInf;
        UsbStorageClassDriverINFFile m_usbStorageClassDriverInf;
        UsbPortINFFile m_usbPortInf;

        public WindowsInstallation(string targetDirectory)
        {
            m_targetDirectory = targetDirectory;
            if (FileSystemUtils.IsDirectoryExist(targetDirectory + "$WIN_NT$.~BT") &&
                    FileSystemUtils.IsDirectoryExist(targetDirectory + "$WIN_NT$.~LS"))
            {
                m_bootDirectory = targetDirectory + "$WIN_NT$.~BT" + "\\";
                m_localSourceDirectory = targetDirectory + "$WIN_NT$.~LS" + "\\";
            }
            else if (FileSystemUtils.IsDirectoryExist(targetDirectory + "I386") ||
                     FileSystemUtils.IsDirectoryExist(targetDirectory + "IA64") ||
                     FileSystemUtils.IsDirectoryExist(targetDirectory + "amd64"))
            {
                m_localSourceDirectory = targetDirectory;
            }

            if (this.IsTargetValid)
            {
                LoadFiles();
            }
        }

        private void LoadFiles()
        {
            m_textSetupInf = new TextSetupINFFile();
            m_textSetupInf.ReadFromDirectory(this.SetupDirectory);

            m_hiveSoftwareInf = new HiveSoftwareINFFile();
            m_hiveSoftwareInf.ReadFromDirectory(this.SetupDirectory);

            if (this.Is64Bit)
            {
                m_hiveSoftware32Inf = new HiveSoftware32INFFile();
                m_hiveSoftware32Inf.ReadFromDirectory(this.SetupDirectory);
            }

            m_hiveSystemInf = new HiveSystemINFFile();
            m_hiveSystemInf.ReadFromDirectory(this.SetupDirectory);

            if (!this.IsTargetContainsTemporaryInstallation)
            {
                // integration to installation media
                m_dosNetInf = new DosNetINFFile();
                m_dosNetInf.ReadFromDirectory(this.SetupDirectory);
            }

            m_setupRegistryHive = new SetupRegistryHiveFile();
            m_netGPCInf = new NetGPCINFFile();
            m_netGPCInf.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);
            m_netPacketSchedulerInf = new NetPacketSchedulerINFFile();
            m_netPacketSchedulerInf.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);
            m_netPacketSchedulerAdapterInf = new NetPacketSchedulerAdapterINFFile();
            m_netPacketSchedulerAdapterInf.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);
            m_netTCPIP = new NetTCPIPINFFile();
            m_netTCPIP.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);
            m_halInf = new HalINFFile();
            m_halInf.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);
            m_usbInf = new UsbINFFile();
            m_usbInf.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);
            m_usbStorageClassDriverInf = new UsbStorageClassDriverINFFile();
            m_usbStorageClassDriverInf.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);

            if (!this.IsWindows2000) // usbport.inf does not exist in Windows 2000
            {
                m_usbPortInf = new UsbPortINFFile();
                m_usbPortInf.ReadPackedCriticalFileFromDirectory(this.SetupDirectory);
            }
        }

        public void SaveModifiedINIFiles()
        {
            if (m_textSetupInf.IsModified)
            {
                m_textSetupInf.SaveToDirectory(this.SetupDirectory);

                if (IsTargetContainsTemporaryInstallation)
                {
                    m_textSetupInf.SaveToDirectory(m_targetDirectory);
                    m_textSetupInf.SaveToDirectory(m_bootDirectory);
                }
            }

            if (HiveSoftwareInf.IsModified)
            {
                m_hiveSoftwareInf.SaveToDirectory(this.SetupDirectory);
            }

            if (this.Is64Bit && HiveSoftware32Inf.IsModified)
            {
                m_hiveSoftware32Inf.SaveToDirectory(this.SetupDirectory);
            }

            if (m_hiveSystemInf.IsModified)
            {
                m_hiveSystemInf.SaveToDirectory(this.SetupDirectory);
            }

            if (!this.IsTargetContainsTemporaryInstallation && m_dosNetInf.IsModified)
            {
                // integration to installation media
                m_dosNetInf.SaveToDirectory(this.SetupDirectory);
            }

            if (m_netGPCInf.IsModified)
            {
                m_netGPCInf.SavePackedToDirectory(this.SetupDirectory);
            }

            if (m_netPacketSchedulerInf.IsModified)
            {
                m_netPacketSchedulerInf.SavePackedToDirectory(this.SetupDirectory);
            }

            if (m_netPacketSchedulerAdapterInf.IsModified)
            {
                m_netPacketSchedulerAdapterInf.SavePackedToDirectory(this.SetupDirectory);
            }

            if (m_netTCPIP.IsModified)
            {
                m_netTCPIP.SavePackedToDirectory(this.SetupDirectory);
            }

            if (m_halInf.IsModified)
            {
                m_halInf.SavePackedToDirectory(this.SetupDirectory);
            }

            if (m_usbInf.IsModified)
            {
                m_usbInf.SavePackedToDirectory(this.SetupDirectory);
            }

            if (m_usbStorageClassDriverInf.IsModified)
            {
                m_usbStorageClassDriverInf.SavePackedToDirectory(this.SetupDirectory);
            }

            if (!IsWindows2000 &&m_usbPortInf.IsModified)
            {
                    m_usbPortInf.SavePackedToDirectory(this.SetupDirectory);   
            }
        }

        public void SaveRegistryChanges()
        {
            m_setupRegistryHive.UnloadHive(true);
            if (this.IsTargetContainsTemporaryInstallation)
            {
                FileSystemUtils.ClearReadOnlyAttribute(m_bootDirectory + m_setupRegistryHive.FileName);
                try
                {
                    ProgramUtils.CopyCriticalFile(this.SetupDirectory + m_setupRegistryHive.FileName, m_bootDirectory + m_setupRegistryHive.FileName);
                }
                catch
                {
                    Console.WriteLine("Error: failed to copy '{0}' to '{1}' (setup boot folder)", m_setupRegistryHive.FileName, m_bootDirectory);
                    Program.Exit();
                }
            }
        }

		/// <summary>
		/// this method will delete migrate.inf, which contains current drive letter assignments.
		/// this step will assure that the system drive letter will be C
		/// </summary>
		public void DeleteMigrationInformation()
		{
			string path = this.BootDirectory + "migrate.inf";
			if (File.Exists(path))
			{
				FileSystemUtils.ClearReadOnlyAttribute(path);
				File.Delete(path);
			}
		}
		
        public void CopyFileFromSetupDirectoryToBootDirectory(string fileName)
        {
            FileSystemUtils.ClearReadOnlyAttribute(m_bootDirectory + fileName);
            File.Copy(this.SetupDirectory + fileName, m_bootDirectory + fileName, true);
        }

        public string GetSetupDriverDirectoryPath(string relativeDriverDirectoryPath)
        {
            string driverSourceDirectory = this.SetupDirectory + ScsiDriversSourceDirectory + relativeDriverDirectoryPath;
            return driverSourceDirectory;
        }

        /// <summary>
        /// Media root has the form of \i386\SCSIDRV\BUSDRV
        /// </summary>
        /// /// <param name="relativeDriverDirectoryPath">Source driver directory relative to \Windows</param>
        public string GetSourceDriverDirectoryInMediaRootForm(string relativeDriverDirectoryPath)
        {
            string driverDirectoryInMediaRootForm = "\\" + this.SetupDirectoryName + "\\" + ScsiDriversSourceDirectory + relativeDriverDirectoryPath;
            driverDirectoryInMediaRootForm = driverDirectoryInMediaRootForm.TrimEnd('\\');
            return driverDirectoryInMediaRootForm.ToLower();
        }

        /// <summary>
        /// has the form of TEMP\SCSIDRV\BUSDRV
        /// </summary>
        /// <param name="relativeDriverDirectoryPath">Target driver directory relative to \Windows</param>
        public string GetDriverDestinationWinntDirectory(string relativeDriverDirectoryPath)
        {
            string driverTargetDirectoryWinnt = ScsiDriversDestinationDirectory + relativeDriverDirectoryPath;
            driverTargetDirectoryWinnt = driverTargetDirectoryWinnt.TrimEnd('\\');
            return driverTargetDirectoryWinnt;
        }

        public void CopyFileToSetupDriverDirectory(string sourceFilePath, string destinationRelativeDirectoryPath, string destinationFileName)
        {
            string destinationDirectoryPath = GetSetupDriverDirectoryPath(destinationRelativeDirectoryPath);
            FileSystemUtils.CreateDirectory(destinationDirectoryPath);
            ProgramUtils.CopyCriticalFile(sourceFilePath, destinationDirectoryPath + destinationFileName);
        }

        // Drivers (.sys) are needed to be copied to the setup dir and boot dir as well
        public void CopyDriverToSetupRootDirectory(string sourceFilePath, string fileName)
        {
            ProgramUtils.CopyCriticalFile(sourceFilePath, this.SetupDirectory + fileName);
        }

        public void AddDriverToBootDirectory(string sourceFilePath, string fileName)
        {
            ProgramUtils.CopyCriticalFile(sourceFilePath, this.SetupDirectory + fileName);
        }

        public string AllocateVirtualDeviceInstanceID(string deviceClassName)
        {
            // we will return the larger deviceInstanceID, we don't want to overwrite existing hivesys.inf device instances
            string deviceInstanceID1 = m_setupRegistryHive.AllocateVirtualDeviceInstanceID(deviceClassName);
            string deviceInstanceID2 = m_hiveSystemInf.AllocateVirtualDeviceInstanceID(deviceClassName);
            if (String.Compare(deviceInstanceID1, deviceInstanceID2) == 1) // string comparison, note that both strings has fixed length with leading zeros
            {
                return deviceInstanceID1;
            }
            else
            {
                return deviceInstanceID2;
            }
        }

        public TextSetupINFFile TextSetupInf
        {
            get
            {
                return m_textSetupInf;
            }
        }

        public HiveSoftwareINFFile HiveSoftwareInf
        {
            get
            {
                return m_hiveSoftwareInf;
            }
        }

        public HiveSoftware32INFFile HiveSoftware32Inf
        {
            get
            {
                return m_hiveSoftware32Inf;
            }
        }

        public HiveSystemINFFile HiveSystemInf
        {
            get
            {
                return m_hiveSystemInf;
            }
        }

        public DosNetINFFile DosNetInf
        {
            get
            {
                return m_dosNetInf;
            }
        }

        public SetupRegistryHiveFile SetupRegistryHive
        {
            get
            {
                return m_setupRegistryHive;
            }
        }

        public NetGPCINFFile NetGPCInf
        {
            get
            {
                return m_netGPCInf;
            }
        }

        public NetPacketSchedulerINFFile NetPacketSchedulerInf
        {
            get
            {
                return m_netPacketSchedulerInf;
            }
        }

        public NetPacketSchedulerAdapterINFFile NetPacketSchedulerAdapterInf
        {
            get
            {
                return m_netPacketSchedulerAdapterInf;
            }
        }

        public NetTCPIPINFFile NetTCPIPInf
        {
            get
            {
                return m_netTCPIP;
            }
        }

        public HalINFFile HalInf
        {
            get
            {
                return m_halInf;
            }
        }

        public UsbINFFile UsbInf
        {
            get
            {
                return m_usbInf;
            }
        }

        public UsbStorageClassDriverINFFile UsbStorageClassDriverInf
        {
            get
            {
                return m_usbStorageClassDriverInf;
            }
        }

        public UsbPortINFFile UsbPortInf
        {
            get
            {
                return m_usbPortInf;
            }
        }

        public bool IsTargetContainsTemporaryInstallation
        {
            get
            {
                return (m_bootDirectory != String.Empty);
            }
        }

        public bool IsTargetValid
        {
            get
            {
                return m_localSourceDirectory != String.Empty;
            }
        }

        public string TargetDirectory
        {
            get
            {
                return m_targetDirectory;
            }
        }

        public string BootDirectory
        {
            get
            {
                return m_bootDirectory;
            }
        }

        public string SetupDirectory
        {
            get
            {
                return m_localSourceDirectory + this.SetupDirectoryName + "\\";
            }
        }

        public bool Is64Bit
        {
            get
            {
                return (this.ArchitectureIdentifier != "x86");
            }
        }

        public string ArchitectureIdentifier
        {
            get
            {
                if (FileSystemUtils.IsDirectoryExist(m_localSourceDirectory + "amd64"))
                {
                    return "amd64";
                }
                else if (FileSystemUtils.IsDirectoryExist(m_localSourceDirectory + "IA64"))
                {
                    return "ia64";
                }
                else
                {
                    return "x86";
                }
            }
        }

        public string SetupDirectoryName
        {
            get
            {
                if (FileSystemUtils.IsDirectoryExist(m_localSourceDirectory + "amd64"))
                {
                    return "amd64";
                }
                else if (FileSystemUtils.IsDirectoryExist(m_localSourceDirectory + "IA64"))
                {
                    return "IA64";
                }
                else
                {
                    return "I386";
                }
            }
        }

        public bool IsWindows2000
        {
            get
            {
                return this.HiveSoftwareInf.GetWindowsProductName().Equals("Microsoft Windows 2000", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsWindowsXP
        {
            get
            {
                return this.HiveSoftwareInf.GetWindowsProductName().Equals("Microsoft Windows XP", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsWindowsServer2003
        {
            get
            {
                return this.HiveSoftwareInf.GetWindowsProductName().Equals("Microsoft Windows Server 2003", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public int MinorOSVersion
        {
            get
            {
                if (IsWindowsServer2003 || (IsWindowsXP && Is64Bit)) // Server 2003 and XP x64 has OS version 5.2
                {
                    return 2;
                }
                else if (IsWindowsXP) // XP x86 has OS version 5.1
                {
                    return 1;
                }
                else // Windows 2000 has OS version 5.0
                {
                    return 0;
                }
            }
        }

        public int ServicePackVersion
        {
            get
            {
                return this.HiveSystemInf.GetWindowsServicePackVersion();
            }
        }

        /// <summary>
        /// 0x0000001 (VER_NT_WORKSTATION) 
        /// 0x0000002 (VER_NT_DOMAIN_CONTROLLER) 
        /// 0x0000003 (VER_NT_SERVER) 
        /// </summary>
        public int ProductType
        { 
            get
            {
                string productTypeString = HiveSystemInf.GetWindowsProductType();
                switch (productTypeString.ToLower())
                {
                    case "winnt":
                        return 1;
                    case "lanmannt":
                        return 2;
                    case "servernt":
                        return 3;
                    default:
                        return 1;
                }
            }
        }
    }
}
