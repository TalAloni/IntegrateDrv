using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;
using Utilities;

namespace IntegrateDrv
{
    public class PNPDriverGUIModeIntegrator : PNPDriverIntegratorBase
    {
        WindowsInstallation m_installation;

        public PNPDriverGUIModeIntegrator(PNPDriverDirectory driverDirectory, WindowsInstallation installation, string hardwareID)
            : base(driverDirectory, installation.ArchitectureIdentifier, installation.MinorOSVersion, installation.ProductType, hardwareID)
        {
            m_installation = installation;
        }

        public void Integrate()
        {
            PNPDriverINFFile pnpDriverInf;
            string installSectionName = this.DriverDirectory.GetDeviceInstallSectionName(this.HardwareID, m_installation.ArchitectureIdentifier, m_installation.MinorOSVersion, m_installation.ProductType, out pnpDriverInf);
            ProcessInstallSection(pnpDriverInf, installSectionName, String.Empty); // We don't care about the classInstanceID because we don't populate the registry
            ProcessCoInstallersSection(pnpDriverInf, installSectionName);
            CopyDriverToSetupDriverDirectoryAndRegisterIt(pnpDriverInf);
        }

        public void CopyDriverToSetupDriverDirectoryAndRegisterIt(PNPDriverINFFile pnpDriverInf)
        {
            Console.WriteLine();
            Console.WriteLine("Making the driver available to GUI mode setup.");

            // build list of source files:
            List<string> driverFiles = new List<string>();
            foreach (FileToCopy fileToCopy in this.DriverFilesToCopy)
            {
                DisableInBoxDeviceDriverFile(m_installation.SetupDirectory, fileToCopy.DestinationFileName);

                driverFiles.Add(fileToCopy.RelativeSourceFilePath);
            }
            
            // make sure the .inf file will be copied too
            driverFiles.Add(pnpDriverInf.FileName);

            if (pnpDriverInf.CatalogFile != String.Empty)
            {
                if (File.Exists(this.DriverDirectory.Path + pnpDriverInf.CatalogFile))
                {
                    // add the catalog file too (to suppress unsigned driver warning message if the .inf has not been modified)
                    // the catalog file is in the same location as the INF file ( http://msdn.microsoft.com/en-us/library/windows/hardware/ff547502%28v=vs.85%29.aspx )
                    driverFiles.Add(pnpDriverInf.CatalogFile);
                }
            }

            // Note that we may perform some operations on the same directory more than once,
            // the allocate / register methods are supposed to return the previously allocated IDs on subsequent calls,
            // and skip registration of previously registered directories
            foreach (string relativeFilePath in driverFiles)
            {
                string fileName = FileSystemUtils.GetNameFromPath(relativeFilePath);
                string relativeDirectoryPath = relativeFilePath.Substring(0, relativeFilePath.Length - fileName.Length);

                // we need to copy the files to the proper sub-directories
                m_installation.CopyFileToSetupDriverDirectory(this.DriverDirectory.Path + relativeDirectoryPath + fileName, this.HardwareID + @"\" + relativeDirectoryPath, fileName);

                string sourceDirectoryInMediaRootForm = m_installation.GetSourceDriverDirectoryInMediaRootForm(this.HardwareID + @"\" + relativeDirectoryPath); // note that we may violate ISO9660 - & is not allowed
                int sourceDiskID = m_installation.TextSetupInf.AllocateSourceDiskID(m_installation.ArchitectureIdentifier, sourceDirectoryInMediaRootForm);

                string destinationWinntDirectory = m_installation.GetDriverDestinationWinntDirectory(this.HardwareID + @"\" + relativeDirectoryPath);
                int destinationWinntDirectoryID = m_installation.TextSetupInf.AllocateWinntDirectoryID(destinationWinntDirectory);

                m_installation.TextSetupInf.SetSourceDisksFileEntry(m_installation.ArchitectureIdentifier, sourceDiskID, destinationWinntDirectoryID, fileName, FileCopyDisposition.AlwaysCopy);
                
                // dosnet.inf: we add the file to the list of files to be copied to local source directory
                if (!m_installation.IsTargetContainsTemporaryInstallation)
                {
                    m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToLocalSourceDriverDirectory(sourceDirectoryInMediaRootForm, fileName);
                }

                m_installation.HiveSoftwareInf.RegisterDriverDirectory(destinationWinntDirectory);
                if (m_installation.Is64Bit)
                {
                    // hivsft32.inf
                    m_installation.HiveSoftware32Inf.RegisterDriverDirectory(destinationWinntDirectory);
                }
            }

            // set inf to boot start:
            string setupDriverDirectoryPath = m_installation.GetSetupDriverDirectoryPath(this.HardwareID + @"\"); // note that we may violate ISO9660 - & character is not allowed
            string installSectionName = pnpDriverInf.GetDeviceInstallSectionName(this.HardwareID, m_installation.ArchitectureIdentifier, m_installation.MinorOSVersion, m_installation.ProductType);
            pnpDriverInf.SetServiceToBootStart(installSectionName, m_installation.ArchitectureIdentifier, m_installation.MinorOSVersion);
            pnpDriverInf.SaveToDirectory(setupDriverDirectoryPath);
            // finished setting inf to boot start
        }

        public override void SetCurrentControlSetRegistryKey(string keyName, string valueName, Microsoft.Win32.RegistryValueKind valueKind, object valueData)
        {
            // Do nothing, we just copy files
        }

        // Windows File Protection may restore a newer unsigned driver file to an older in-box signed driver file (sfc.exe is executed at the end of GUI-mode setup).
        // The list of files that is being protected is stored in sfcfiles.sys, and we can prevent a file from being protected by making sure it's not in that list.
        public static void DisableInBoxDeviceDriverFile(string setupDirectory, string fileName)
        {
            fileName = fileName.ToLower(); // sfcfiles.dll stores all file names in lowercase
            string path = setupDirectory + "sfcfiles.dl_";
            byte[] packed = File.ReadAllBytes(path);
            byte[] unpacked = HiveINIFile.Unpack(packed, "sfcfiles.dll");
            PortableExecutableInfo peInfo = new PortableExecutableInfo(unpacked);
            string oldValue = @"%systemroot%\system32\drivers\" + fileName;
            string newValue = @"%systemroot%\system32\drivers\" + fileName.Substring(0, fileName.Length - 1) + "0"; // e.g. e1000325.sys => e1000325.sy0
            byte[] oldSequence = Encoding.Unicode.GetBytes(oldValue);
            byte[] newSequence = Encoding.Unicode.GetBytes(newValue);

            bool replaced = false;
            for (int index = 0; index < peInfo.Sections.Count; index++)// XP uses the .text section while Windows 2000 uses the .data section
            {
                byte[] section = peInfo.Sections[index];
                bool replacedInSection = KernelAndHalIntegrator.ReplaceInBytes(ref section, oldSequence, newSequence);
                
                if (replacedInSection)
                {
                    peInfo.Sections[index] = section;
                    replaced = true;
                }
            }

            if (replaced)
            {
                Console.WriteLine();
                Console.WriteLine("'{0}' has been removed from Windows File Protection file list.", fileName);
                
                MemoryStream peStream = new MemoryStream();
                PortableExecutableInfo.WritePortableExecutable(peInfo, peStream);
                unpacked = peStream.ToArray();
                packed = HiveINIFile.Pack(unpacked, "sfcfiles.dll");

                FileSystemUtils.ClearReadOnlyAttribute(path);
                File.WriteAllBytes(path, packed);
            }
        }

        // In-box device drivers = drivers that are shipped with Windows
        public static void DisableInBoxDeviceDrivers(string setupDirectory, string architectureIdentifier, int minorOSVersion, int productType, string hardwareID)
        {
            Console.WriteLine();
            Console.WriteLine("Looking for drivers for your device (" + hardwareID + ") in Windows setup directory (to disable them):");
            string[] filePaths = Directory.GetFiles(setupDirectory, "*.in_");
            foreach (string filePath in filePaths)
            {
                string packedFileName = FileSystemUtils.GetNameFromPath(filePath);
                string unpackedFileName = packedFileName.Substring(0, packedFileName.Length - 1) + "F"; // the filename inside the archive ends with .INF and not with .IN_

                CabInfo cabInfo = new CabInfo(filePath);

                ArchiveFileInfo fileInfo = null;
                try
                {
                    // some files do not contain an inf file
                    // for instance, netmon.in_ contains netmon.ini
                    fileInfo = cabInfo.GetFile(unpackedFileName);
                }
                catch (CabException ex)
                {
                    // file is invalid / unsupported
                    Console.WriteLine("Cannot examine file '{0}': {1}", packedFileName, ex.Message);
                }

                if (fileInfo != null)
                {
                    PNPDriverINFFile driverInf = new PNPDriverINFFile(unpackedFileName);
                    try
                    {
                        driverInf.ReadPackedFromDirectory(setupDirectory);
                    }
                    catch (CabException ex)
                    {
                        // the archive is a cab and it contains the file we are looking for, but the file is corrupted
                        Console.WriteLine("Cannot unpack file '{0}': {1}", driverInf.PackedFileName, ex.Message);
                        continue;
                    }

                    // Windows will pick up it's own signed drivers even if the added driver also match the SUBSYS,
                    // so we have to disable in-box drivers regardless of the presence of &SUBSYS
                    bool found = driverInf.DisableMatchingHardwareID(hardwareID, architectureIdentifier, minorOSVersion, productType);

                    if (found)
                    {
                        Console.WriteLine("Device driver for '{0}' found in file '{1}'.", hardwareID, packedFileName);
                        driverInf.SavePackedToDirectory(setupDirectory);
                    }
                }
            }
            Console.WriteLine("Finished looking for drivers for your device in Windows setup directory.");
        }
    }
}
