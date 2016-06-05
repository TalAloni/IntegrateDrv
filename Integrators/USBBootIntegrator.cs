using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class USBBootIntegrator
    {
        private WindowsInstallation m_installation;

        public USBBootIntegrator(WindowsInstallation installation)
        {
            m_installation = installation;
        }

        /// <summary>
        /// Successful USB 2.0 Boot: Inbox USB 2.0 host controller and hub Drivers   + USB mass storage class driver (usbstor.sys).
        /// Successful USB 3.0 Boot: Vendor provided host controller anb hub drivers + USB mass storage class driver (usbstor.sys).
        /// In addition, most systems will require an additional step in the form of a driver that will wait for the USB storage device to be initialized,
        /// I have written Wait4UFD for that purpose.
        /// See: http://msdn.microsoft.com/en-us/library/ee428799.aspx
        ///      http://reboot.pro/topic/14427-waitbt-for-usb-booting/
        /// </summary>
        public void IntegrateUSB20HostControllerAndHubDrivers()
        {
            if (m_installation.IsWindows2000)
            {
                // The service pack version must be SP4 for the /usbboot switch to get accepted
                if (!FileSystemUtils.IsFileExist(m_installation.SetupDirectory + "sp4.cab"))
                {
                    Console.WriteLine("Error: Missing file: sp4.cab");
                    Program.Exit();
                }

                // Note: Windows 2000 SP4 added USB 2.0 support, earlier versions are limited to USB 1.1
                // Copy USB 2.0 filles from SP4.cab:
                string path = m_installation.SetupDirectory + "sp4.cab";
                byte[] packed = File.ReadAllBytes(path);
                byte[] unpacked = HiveINIFile.Unpack(packed, "usbehci.sys");
                File.WriteAllBytes(m_installation.BootDirectory + "usbehci.sys", unpacked);
                unpacked = HiveINIFile.Unpack(packed, "usbport.sys");
                File.WriteAllBytes(m_installation.BootDirectory + "usbport.sys", unpacked);
                unpacked = HiveINIFile.Unpack(packed, "usbhub20.sys");
                File.WriteAllBytes(m_installation.BootDirectory + "usbhub20.sys", unpacked);

                // Create the [files.usbehci] and [files.usbhub20] sections.
                // Those sections lists the driver files that will be copied if the device has been detected.
                m_installation.TextSetupInf.AddFileToFilesSection("files.usbehci", "hid.dll", (int)WinntDirectoryName.System32);
                m_installation.TextSetupInf.AddFileToFilesSection("files.usbehci", "hccoin.dll", (int)WinntDirectoryName.System32);
                m_installation.TextSetupInf.AddFileToFilesSection("files.usbehci", "hidclass.sys", (int)WinntDirectoryName.System32);
                m_installation.TextSetupInf.AddFileToFilesSection("files.usbehci", "hidparse.sys", (int)WinntDirectoryName.System32_Drivers);
                m_installation.TextSetupInf.AddFileToFilesSection("files.usbehci", "usbd.sys", (int)WinntDirectoryName.System32_Drivers);
                m_installation.TextSetupInf.AddFileToFilesSection("files.usbehci", "usbport.sys", (int)WinntDirectoryName.System32_Drivers);
                m_installation.TextSetupInf.AddFileToFilesSection("files.usbehci", "usbehci.sys", (int)WinntDirectoryName.System32_Drivers);

                m_installation.TextSetupInf.AddFileToFilesSection("files.usbhub20", "usbhub20.sys", (int)WinntDirectoryName.System32_Drivers);

                RegisterBootBusExtender("usbehci", "Enhanced Host Controller", "files.usbehci");
                m_installation.UsbInf.SetWindows2000UsbEnhancedHostControllerToBootStart();
                
                m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("openhci");
                RegisterBootBusExtender("openhci", "Open Host Controller", "files.openhci");
                m_installation.UsbInf.SetWindows2000OpenHostControllerToBootStart();

                m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("uhcd");
                RegisterBootBusExtender("uhcd", "Universal Host Controller", "files.uhcd");
                m_installation.UsbInf.SetWindows2000UsbUniversalHostControllerToBootStart();

                m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("usbhub");
                RegisterBootBusExtender("usbhub", "Generic USB Hub Driver", "files.usbhub");
                RegisterBootBusExtender("usbhub20", "Generic USB Hub Driver", "files.usbhub20");
                m_installation.UsbInf.SetWindows2000UsbRootHubToBootStart();
                m_installation.UsbInf.SetWindows2000Usb20RootHubToBootStart();
                m_installation.UsbInf.SetWindows2000Usb20GenericHubToBootStart();

                // Update the CDDB
                m_installation.TextSetupInf.AddDeviceToCriticalDeviceDatabase(@"PCI\CC_0C0320", "usbehci");
                m_installation.TextSetupInf.AddDeviceToCriticalDeviceDatabase(@"USB\ROOT_HUB20", "usbhub20");
                // Needed in case the user uses a USB 2.0 Hub between the UFD and the root hub:
                // Note: Under Windows 2000 SP4, 'USB\HubClass' is the identifier of USB 2.0 Hubs.
                m_installation.TextSetupInf.AddDeviceToCriticalDeviceDatabase(@"USB\HubClass", "usbhub20");
                // Note: I could not get usbhub.sys and usbhub20.sys working consistently at the same time under text-mode setup.
                // as a solution, we must prevent usbhub.sys from being loaded (USB 1.x devices such as mouse / keyboard may not work)
                m_installation.TextSetupInf.RemoveDeviceFromCriticalDeviceDatabase(@"USB\COMPOSITE");
                m_installation.TextSetupInf.RemoveDeviceFromCriticalDeviceDatabase(@"USB\ROOT_HUB");
                m_installation.TextSetupInf.RemoveDeviceFromCriticalDeviceDatabase(@"USB\CLASS_09&SUBCLASS_01");
                m_installation.TextSetupInf.RemoveDeviceFromCriticalDeviceDatabase(@"USB\CLASS_09");

                Console.Write("********************************************************************************");
                Console.Write("*The author was not able to get USB 1.x and USB 2.0 consistently working at the*");
                Console.Write("*same time during Windows 2000 text-mode setup. USB 1.x has been temporarily   *");
                Console.Write("*disabled, devices such as USB mouse or keyboard may not work as a result.     *");
                Console.Write("*USB 1.x will be re-enabled during GUI-mode setup.                             *");
                Console.Write("********************************************************************************");
                // One notable exception to the above was when a USB 2.0 hub was connected between the UFD and the onboard USB port:
                // When connected directly, the I got a BSOD, but when connected to the same port through a USB 2.0 hub
                // ('USB\ROOT_HUB' was set to load usbhub.sys, 'USB\CLASS_09' had to be removed from the CDDB), Both USB 1.x and USB 2.0 devices worked properly.
            }
            else
            {
                m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("usbehci");
                RegisterBootBusExtender("usbehci", "Enhanced Host Controller", "files.usbehci");
                m_installation.UsbPortInf.SetUsbEnhancedHostControllerToBootStart();

                m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("usbohci");
                RegisterBootBusExtender("usbohci", "Open Host Controller", "files.usbohci");
                m_installation.UsbPortInf.SetUsbUniversalHostControllerToBootStart();

                m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("usbuhci");
                RegisterBootBusExtender("usbuhci", "Universal Host Controller", "files.usbuhci");
                m_installation.UsbPortInf.SetUsbOpenHostControllerToBootStart();

                m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("usbhub");
                RegisterBootBusExtender("usbhub", "Generic USB Hub Driver", "files.usbhub");
                m_installation.UsbPortInf.SetUsbRootHubToBootStart();
                // The root hub and generic hub have two different INF files, but both use the same service,
                // so if a generic USB 2.0 hub is detected, the service will be reinstalled.
                // We must make sure the service will still be set to boot start during this process.
                m_installation.UsbInf.SetWindowsXP2003UsbStandardHubToBootStart();
            }

            // Add the generic USB hub to the final CDDB, in case our user will decide to add
            // a hub between the UFD and the root hub after the installation.
            // see: http://www.usb.org/developers/defined_class
            m_installation.HiveSystemInf.AddDeviceToCriticalDeviceDatabase(@"USB\Class_09", "usbhub");
            if (m_installation.IsWindows2000)
            {
                // Windows 2000 uses usbhub.sys for USB 1.x hubs, and usbhub20.sys for USB 2.0 hubs.
                m_installation.HiveSystemInf.AddDeviceToCriticalDeviceDatabase(@"USB\HubClass", "usbhub20");
            }
        }

        public void IntegrateUSBStorageDriver()
        {
            m_installation.TextSetupInf.RemoveInputDevicesSupportDriverLoadInstruction("usbstor");
            RegisterBootBusExtender("usbstor","USB Storage Class Driver","files.usbstor");
            m_installation.UsbStorageClassDriverInf.SetUsbStorageClassDriverToBootStart();
        }

        private void RegisterBootBusExtender(string serviceName, string serviceDisplayName, string filesSectionName)
        {
            string fileName = serviceName + ".sys";

            m_installation.TextSetupInf.InstructToLoadBootBusExtenderDriver(fileName, serviceDisplayName, filesSectionName);

            int errorControl = 1;
            string group = "Boot Bus Extender";
            int start = 0;
            int type = 1;
            string imagePath = @"system32\drivers\" + fileName;
            RegisterDeviceService(m_installation.SetupRegistryHive, serviceName, serviceDisplayName, errorControl, group, start, type, imagePath);
            RegisterDeviceService(m_installation.HiveSystemInf, serviceName, serviceDisplayName, errorControl, group, start, type, imagePath);
        }

        private void RegisterDeviceService(ISystemRegistryHive systemRegistryHive, string serviceName, string serviceDisplayName, int errorControl, string group, int start, int type, string imagePath)
        {
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "DisplayName", RegistryValueKind.String, serviceDisplayName);
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "ErrorControl", RegistryValueKind.DWord, 1);
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "Group", RegistryValueKind.String, "Boot Bus Extender");
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "Start", RegistryValueKind.DWord, 0);
            systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "Type", RegistryValueKind.DWord, 1);

            if (systemRegistryHive is HiveSystemINFFile) // GUI Mode registry
            {
                systemRegistryHive.SetServiceRegistryKey(serviceName, String.Empty, "ImagePath", RegistryValueKind.String, imagePath);
            }
        }
    }
}
