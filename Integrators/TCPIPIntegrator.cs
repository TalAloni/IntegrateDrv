using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class TCPIPIntegrator
    {
        private WindowsInstallation m_installation;
        private List<NetworkDeviceService> m_netDeviceServices;

        public TCPIPIntegrator(WindowsInstallation installation, List<NetworkDeviceService> netDeviceServices)
        {
            m_installation = installation;
            m_netDeviceServices = netDeviceServices;
        }

        public void SetTCPIPBoot()
        {
            PrepareTextModeToTCPIPBootStart();

            SetRegistryToTCPIPBootStart(m_installation.SetupRegistryHive);
            SetRegistryToTCPIPBootStart(m_installation.HiveSystemInf);

            SetTCPIPSetupToBootStartForGUIMode();
        }

        private void PrepareTextModeToTCPIPBootStart()
        {
            // Copy the necessary files:
            if (m_installation.IsTargetContainsTemporaryInstallation)
            {
                m_installation.CopyFileFromSetupDirectoryToBootDirectory("tdi.sy_"); // dependency of tcpip.sys
                m_installation.CopyFileFromSetupDirectoryToBootDirectory("ndis.sy_"); // dependency of tcpip.sys
                if (!m_installation.IsWindows2000) // Windows 2000 has an independent Tcpip service that does not require IPSec
                {
                    m_installation.CopyFileFromSetupDirectoryToBootDirectory("ipsec.sy_");
                }
                m_installation.CopyFileFromSetupDirectoryToBootDirectory("tcpip.sy_");
            }
            else
            {
                // Update DosNet.inf
                m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory("tdi.sy_");
                m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory("ndis.sy_");
                if (!m_installation.IsWindows2000) // Windows 2000 has an independent Tcpip service that does not require IPSec
                {
                    m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory("ipsec.sy_");
                }
                m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory("tcpip.sy_");
            }

            // update txtsetup.sif:

            // We must make sure NDIS is initialized before the NIC (does it have to be loaded before the NIC too?)

            // a solution to the above is to use the [BusExtender.Load] section, 
            // Since BusExtenders are loaded before almost everything else (setupdd.sys -> BootBusExtenders -> BusExtenders ...) it's a great place for NDIS,
            // another advantage of the [BusExtender.Load] section is that it's not sticky. (see TextSetupINFFile.Load.cs)
            
            m_installation.TextSetupInf.InstructToLoadBusExtenderDriver("ndis.sys", "NDIS");

            // [DiskDrivers] is not sticky as well, we use [DiskDrivers], but it doesn't really matter because we specify group later
            if (!m_installation.IsWindows2000) // Windows 2000 has an independent Tcpip service that does not require IPSec
            {
                m_installation.TextSetupInf.InstructToLoadDiskDriversDriver("ipsec.sys", "IPSEC Driver");
            }
            m_installation.TextSetupInf.InstructToLoadDiskDriversDriver("tcpip.sys", "TCP/IP Protocol Driver");
            
            // Note about critical files for iSCSI boot:
            // ksecdd.is critical
            // pci.sys is critical
            // partmgr.sys is critical (and its dependency wmilib.sys)
            // disk.sys is critical (and its dependency classpnp.sys)
        }

        /// <summary>
        /// Update the registry, set TCP/IP related services to boot start
        /// </summary>
        private void SetRegistryToTCPIPBootStart(ISystemRegistryHive systemRegistryHive)
        {
            int serviceBootStart = 0;

            if (systemRegistryHive is SetupRegistryHiveFile)
            {
                // text-mode:
                m_installation.SetupRegistryHive.SetServiceRegistryKey("KSecDD", String.Empty, "Start", RegistryValueKind.DWord, serviceBootStart);
                m_installation.SetupRegistryHive.SetServiceRegistryKey("KSecDD", String.Empty, "Group", RegistryValueKind.String, "Base");

                m_installation.SetupRegistryHive.SetServiceRegistryKey("NDIS", String.Empty, "Start", RegistryValueKind.DWord, serviceBootStart);
                m_installation.SetupRegistryHive.SetServiceRegistryKey("NDIS", String.Empty, "Group", RegistryValueKind.String, "NDIS Wrapper");
            }
            // GUI-mode: KSecDD is already taken care of by default
            // GUI-mode: NDIS is already taken care of by default

            if (!m_installation.IsWindows2000) // Windows 2000 has an independent Tcpip service that does not require IPSec
            {
                systemRegistryHive.SetServiceRegistryKey("IPSec", String.Empty, "Start", RegistryValueKind.DWord, serviceBootStart);
                systemRegistryHive.SetServiceRegistryKey("IPSec", String.Empty, "Group", RegistryValueKind.String, "PNP_TDI"); // this will be ignored, text-mode setup will assign Tcpip to 'SCSI Miniport' (because that's where we put it in txtsetup.sif)
                systemRegistryHive.SetServiceRegistryKey("IPSec", String.Empty, "Type", RegistryValueKind.DWord, 1); // SERVICE_KERNEL_DRIVER
            }

            systemRegistryHive.SetServiceRegistryKey("Tcpip", String.Empty, "Start", RegistryValueKind.DWord, serviceBootStart);
            systemRegistryHive.SetServiceRegistryKey("Tcpip", String.Empty, "Group", RegistryValueKind.String, "PNP_TDI"); // this will be ignored, text-mode setup will assign IPSec to 'SCSI Miniport' (because that's where we put it in txtsetup.sif)
            systemRegistryHive.SetServiceRegistryKey("Tcpip", String.Empty, "Type", RegistryValueKind.DWord, 1); // SERVICE_KERNEL_DRIVER
            if (!m_installation.IsWindows2000) // Windows 2000 has an independent Tcpip service that does not require IPSec
            {
                // not absolutely necessary apparently. however. it's still a good practice:
                systemRegistryHive.SetServiceRegistryKey("Tcpip", String.Empty, "DependOnService", RegistryValueKind.String, "IPSec");
            }

            // Needed for stability
            systemRegistryHive.SetCurrentControlSetRegistryKey(@"Control\Session Manager\Memory Management", "DisablePagingExecutive", RegistryValueKind.DWord, 1);
            // not sure that's really necessary, but sanbootconf setup does it, so it seems like a good idea (1 by default for Server 2003)
            systemRegistryHive.SetServiceRegistryKey("Tcpip", "Parameters", "DisableDHCPMediaSense", RegistryValueKind.DWord, 1);

            List<string> bootServices = new List<string>(new string[] { "NDIS Wrapper", "NDIS", "Base", "PNP_TDI" });
            systemRegistryHive.AddServiceGroupsAfterSystemBusExtender(bootServices);
        }

        /// <summary>
        /// This will make sure that TCP/IP will still be marked as a boot service once windows installs it during GUI mode setup
        /// </summary>
        private void SetTCPIPSetupToBootStartForGUIMode()
        {
            if (!m_installation.IsWindows2000) // Windows 2000 has an independent Tcpip service that does not require IPSec
            {
                // Windows XP \ Server 2003: IPSec is required for TCPIP to work
                m_installation.NetTCPIPInf.SetIPSecToBootStart();
            }
            m_installation.NetTCPIPInf.SetTCPIPToBootStart();

            // By default, QoS is not installed with Windows 2000 \ Server 2003
            // Note: Even if we enable it for Windows 2003, installing it manually will break the iSCSI connection and will cause a BSOD.
            if (m_installation.IsWindowsXP)
            {
                // 'General Packet Classifier' is required for 'Packet Scheduler' to work
                // 'Packet Scheduler' is a key function of quality of service (QoS), which is installed by default for Windows XP
                m_installation.NetGPCInf.SetGPCToBootStart();
                m_installation.NetPacketSchedulerAdapterInf.SetPacketSchedulerAdapterToBootStart();

                // Not sure about the role of netpschd.inf, note that it doesn't contain an AddService entry.
                //m_installation.NetPacketSchedulerInf.SetPacketSchedulerToBootStart();
            }
        }

        public void AssignIPAddressToNetDeviceServices(bool staticIP)
        {
            foreach (NetworkDeviceService netDeviceService in m_netDeviceServices)
            {
                AssignIPAddressToNetDeviceService(netDeviceService, staticIP);
            }
        }

        private void AssignIPAddressToNetDeviceService(NetworkDeviceService netDeviceService, bool staticIP)
        {
            string ipAddress;
            string subnetMask;
            string defaultGateway;
            if (staticIP)
            {
                Console.WriteLine("Please select TCP/IP settings for '" + netDeviceService.DeviceDescription + "':");
                Console.WriteLine("* Pressing Enter will default to 192.168.1.50 / 255.255.255.0 / 192.168.1.1");
                ipAddress = ReadValidIPv4Address("IP Address", "192.168.1.50");
                subnetMask = ReadValidIPv4Address("Subnet Mask", "255.255.255.0");
                defaultGateway = ReadValidIPv4Address("Default Gateway", "192.168.1.1");
            }
            else
            {
                ipAddress = "0.0.0.0";
                subnetMask = "0.0.0.0";
                defaultGateway = String.Empty;
            }

            AssignIPAddressToNetDeviceService(netDeviceService, m_installation.SetupRegistryHive, ipAddress, subnetMask, defaultGateway);
            AssignIPAddressToNetDeviceService(netDeviceService, m_installation.HiveSystemInf, ipAddress, subnetMask, defaultGateway);
        }
        
        private void AssignIPAddressToNetDeviceService(NetworkDeviceService netDeviceService, ISystemRegistryHive systemRegistryHive,  string ipAddress, string subnetMask, string defaultGateway)
        {
            string netCfgInstanceID = netDeviceService.NetCfgInstanceID;

            string adapterKeyName = @"Parameters\Adapters\" + netCfgInstanceID;
            string adapterIPConfig = @"Tcpip\Parameters\Interfaces\" + netCfgInstanceID;
            string interfaceKeyName = @"Parameters\Interfaces\" + netCfgInstanceID;

            // this is some kind of reference to where the actual TCP/IP configuration is located
            systemRegistryHive.SetServiceRegistryKey("Tcpip", adapterKeyName, "IpConfig", RegistryValueKind.MultiString, new string[] { adapterIPConfig });

            // DefaultGateway is not necessary for most people, but can ease the installation for people with complex networks
            systemRegistryHive.SetServiceRegistryKey("Tcpip", interfaceKeyName, "DefaultGateway", RegistryValueKind.MultiString, new string[] { defaultGateway });
            // Extracurricular note: it's possible to use more than one IP address, but you have to specify subnet mask for it as well.
            systemRegistryHive.SetServiceRegistryKey("Tcpip", interfaceKeyName, "IPAddress", RegistryValueKind.MultiString, new string[] { ipAddress });
            systemRegistryHive.SetServiceRegistryKey("Tcpip", interfaceKeyName, "SubnetMask", RegistryValueKind.MultiString, new string[] { subnetMask });

            // Note related to GUI mode:
            // We already bind the device class instance to NetCfgInstanceID, and that's all that's necessary for TCP/IP to work during text-mode.
            // However, TCP/IP must be bound to NetCfgInstanceID as well, TCP/IP will work in GUI mode without it, but setup will fail at T-39
            // with the following error: "setup was unable to initialize Network installation components. the specific error code is 2"
            // and in the subsequent screen: "LoadLibrary returned error 1114 (45a)" (related to netman.dll, netshell.dll)

            // The first component in one entry corresponds to the first component in the other entries:
            systemRegistryHive.SetServiceRegistryKey("Tcpip", "Linkage", "Bind", RegistryValueKind.MultiString, new string[] { @"\DEVICE\" + netCfgInstanceID });
            systemRegistryHive.SetServiceRegistryKey("Tcpip", "Linkage", "Export", RegistryValueKind.MultiString, new string[] { @"\DEVICE\TCPIP_" + netCfgInstanceID });
            // NetCfgInstanceID should be quoted, HiveSystemInf should take care of the use of quote characters (special character):
            systemRegistryHive.SetServiceRegistryKey("Tcpip", "Linkage", "Route", RegistryValueKind.MultiString, new string[] { QuotedStringUtils.Quote(netCfgInstanceID) });
        }

        private static string ReadValidIPv4Address(string name, string defaultAddress)
        {
            int retry = 2;
            while (retry > 0)
            {
                System.Console.Write(name + ": ");
                string address = Console.ReadLine();
                if (address.Trim() == String.Empty)
                {
                    address = defaultAddress;
                    Console.WriteLine("Defaulting to " + defaultAddress);
                    return address;
                }
                else if (!ValidadeAddress(address))
                {
                    Console.WriteLine("Invalid " + name + " detected. you have to use IPv4!");
                    retry--;
                }
                else
                {
                    return address;
                }
            }

            Console.WriteLine("Invalid " + name + " detected, aborting!");
            Program.Exit();

            return defaultAddress;
        }

        // We won't use IPAddress.Parse because it's a dependency we do not want or need
        private static bool ValidadeAddress(string address)
        {
            string[] components = address.Split('.');
            if (components.Length != 4)
            {
                return false;
            }
            else
            {
                int arg1 = Conversion.ToInt32(components[0], -1);
                int arg2 = Conversion.ToInt32(components[1], -1);
                int arg3 = Conversion.ToInt32(components[2], -1);
                int arg4 = Conversion.ToInt32(components[3], -1);
                if (arg1 < 0 || arg2 < 0 || arg3 < 0 || arg4 < 0 ||
                    arg1 > 255 || arg2 > 255 || arg3 > 255 || arg4 > 255)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
