using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class Program
    {
        static void Main(string[] args)
        {
            args = CommandLineParser.GetCommandLineArgsIgnoreEscape();
            WindowsInstallation installation;
            List<TextModeDriverDirectory> textModeDriverDirectories;
            List<PNPDriverDirectory> pnpDriverDirectories;
            bool useLocalHardwareConfig;
            string enumExportPath;
            bool preconfigure;
            bool staticIP;
            bool usbBoot;

            bool parseSuccess = ParseCommandLineSwitches(args, out installation, out textModeDriverDirectories, out pnpDriverDirectories, out useLocalHardwareConfig, out enumExportPath, out preconfigure, out staticIP, out usbBoot);
            if (!parseSuccess)
            {
                // parser was already supposed to print an error meesage;
                return;
            }

            // Make sure Windows temporary folder exist (the user may have deleted it)
            if (!FileSystemUtils.IsDirectoryExist(Path.GetTempPath()))
            {
                FileSystemUtils.CreateDirectory(Path.GetTempPath());
            }
            installation.SetupRegistryHive.LoadHiveFromDirectory(installation.SetupDirectory);
            TextModeDriverIntegrator.IntegrateTextModeDrivers(textModeDriverDirectories, installation);
            List<DeviceService> deviceServices = PNPDriverIntegratorUtils.IntegratePNPDrivers(pnpDriverDirectories, installation, useLocalHardwareConfig, enumExportPath, preconfigure);
            List<NetworkDeviceService> netDeviceServices = DeviceServiceUtils.FilterNetworkDeviceServices(deviceServices);

            if (netDeviceServices.Count > 0)
            {
                if (!preconfigure && !DeviceServiceUtils.ContainsService(deviceServices, "nicbtcfg"))
                {
                    Console.WriteLine();
                    Console.Write("********************************************************************************");
                    Console.Write("*You have supplied a device driver for a network adapter, which requires       *");
                    Console.Write("*special registry configuration to support boot start, but you have not        *");
                    Console.Write("*used the /preconf switch. IntegrateDrv will assume you're using NICBootConf   *");
                    Console.Write("*to configure your network adapter during boot. (recommended)                  *");
                    Console.Write("********************************************************************************");
                }
            }

            if (usbBoot && !DeviceServiceUtils.ContainsService(deviceServices, "wait4ufd"))
            {
                Console.WriteLine();
                Console.Write("********************************************************************************");
                Console.Write("*When booting from a USB storage device, most systems will require that you    *");
                Console.Write("*will use a special driver (such as Wait4UFD) that will wait for the UFD boot  *");
                Console.Write("*storage device to be initialized before proceeding with the boot process.     *");
                Console.Write("********************************************************************************");
            }

            if (DeviceServiceUtils.ContainsService(deviceServices, "sanbootconf"))
            {
                Console.WriteLine();
                Console.WriteLine("sanbootconf detected, GUI boot (Windows logo) has been enabled.");

                installation.TextSetupInf.EnableGUIBoot();
            }

            if (netDeviceServices.Count > 0)
            {
                KernelAndHalIntegrator kernelAndHalIntegrator = new KernelAndHalIntegrator(installation);
                kernelAndHalIntegrator.UseUniprocessorKernel();

                Console.WriteLine();
                Console.WriteLine("Network adapter has been added, adding TCP/IP:");
                TCPIPIntegrator integrator = new TCPIPIntegrator(installation, netDeviceServices);
                integrator.SetTCPIPBoot();
                integrator.AssignIPAddressToNetDeviceServices(staticIP);
            }

            if (usbBoot)
            {
                USBBootIntegrator integrator = new USBBootIntegrator(installation);
                Console.WriteLine();
                Console.WriteLine("Integrating USB 2.0 Host Controller and Hub drivers.");
                integrator.IntegrateUSB20HostControllerAndHubDrivers();
                Console.WriteLine("Integrating USB Mass Storage Class Driver.");
                integrator.IntegrateUSBStorageDriver();
            }
			
			// no need to keep migration information (current drive letter assignments)
			installation.DeleteMigrationInformation();

            Console.WriteLine("Committing changes.");
            installation.SaveModifiedINIFiles();
            installation.SaveRegistryChanges();
            Console.WriteLine("Driver integration completed.");
        }

        /// <summary>
        /// return false if args are invalid
        /// </summary>
        private static bool ParseCommandLineSwitches(string[] args, out WindowsInstallation installation, out List<TextModeDriverDirectory> textModeDriverDirectories, out List<PNPDriverDirectory> pnpDriverDirectories, out bool useLocalHardwareConfig, out string enumExportPath, out bool preconfigure, out bool staticIP, out bool usbBoot)
        {
            installation = null;
            textModeDriverDirectories = new List<TextModeDriverDirectory>();
            pnpDriverDirectories = new List<PNPDriverDirectory>();
            enumExportPath = String.Empty;
            useLocalHardwareConfig = false;
            preconfigure = false;
            staticIP = true;
            usbBoot = false;

            List<string> pnpDriverPaths = new List<string>();
            List<string> textModeDriverPaths = new List<string>();
            string targetPath = "C:\\";

            if (args.Length == 0)
            {
                ShowHelp();
                return false;
            }
            else
            {
                foreach (string arg in args)
                {
                    string switchName = arg;
                    string switchParameter = String.Empty;
                    if (arg.StartsWith("/"))
                    {
                        switchName = arg.Substring(1);
                        int index = switchName.IndexOfAny(new char[]{':','='});
                        if (index >= 0)
                        {
                            switchParameter = switchName.Substring(index + 1);
                            switchParameter = switchParameter.Trim('"');
                            switchName = switchName.Substring(0, index);
                        }
                    }

                    switch (switchName.ToLower())
                    {
                        case "driver":
                            string pnpDriverPath = switchParameter;
                            if (!pnpDriverPath.EndsWith("\\") && !pnpDriverPath.EndsWith(":"))
                            {
                                pnpDriverPath = pnpDriverPath + "\\";
                            }
                            pnpDriverPaths.Add(pnpDriverPath);
                            break;
                        case "textmodedriver":
                            string textModeDriverPath = switchParameter;
                            if (!textModeDriverPath.EndsWith("\\") && !textModeDriverPath.EndsWith(":"))
                            {
                                textModeDriverPath = textModeDriverPath + "\\";
                            }
                            textModeDriverPaths.Add(textModeDriverPath);
                            break;
                        case "target":
                            targetPath = switchParameter;
                            if (!targetPath.EndsWith("\\") && !targetPath.EndsWith(":"))
                            {
                                targetPath = targetPath + "\\";
                            }
                            break;
                        case "local":
                            useLocalHardwareConfig = true;
                            break;
                        case "enum":
                            enumExportPath = switchParameter;
                            break;
                        case "preconf":
                            preconfigure = true;
                            break;
                        case "dhcp":
                            staticIP = false;
                            break;
                        case "usbboot":
                            usbBoot = true;
                            break;
                        case "?":
                        case "help":
                            ShowHelp();
                            return false;
                        default:
                            Console.WriteLine("Error: Invalid command-line switch: " + switchName);
                            return false;
                    }
                }

                installation = new WindowsInstallation(targetPath);
                if (!installation.IsTargetValid)
                {
                    Console.WriteLine("Error: Could not find installation directory");
                    Console.WriteLine("- if you are using winnt32.exe, you should use the /makelocalsource switch.");
                    return false;
                }

                if (!installation.IsWindows2000 && !installation.IsWindowsXP && !installation.IsWindowsServer2003)
                {
                    Console.WriteLine("Error: Unsupported operating system version");
                    return false;
                }

                if (enumExportPath == String.Empty && !useLocalHardwareConfig && preconfigure)
                {
                    Console.WriteLine("Error: The /preconf switch can only be present with the /local or /enum switch.");
                    return false;
                }

                foreach (string textModeDriverPath in textModeDriverPaths)
                {
                    TextModeDriverDirectory textModeDriverDirectory = new TextModeDriverDirectory(textModeDriverPath);
                    if (!textModeDriverDirectory.ContainsOEMSetupFile)
                    {
                        Console.WriteLine("Error: Could not find the OEM driver setup file (txtsetup.oem). Directory: " + textModeDriverPath);
                        return false;
                    }
                    textModeDriverDirectories.Add(textModeDriverDirectory);
                }

                foreach (string pnpDriverPath in pnpDriverPaths)
                {
                    PNPDriverDirectory driverDirectory = new PNPDriverDirectory(pnpDriverPath);
                    if (!driverDirectory.ContainsINFFiles)
                    {
                        Console.WriteLine("Error: Could not find any .inf file in driver directory: " + pnpDriverPath);
                        return false;
                    }
                    pnpDriverDirectories.Add(driverDirectory);
                }

                if (enumExportPath != String.Empty && !FileSystemUtils.IsFileExist(enumExportPath))
                {
                    Console.WriteLine("Error: the file '{0}' does not exist.", enumExportPath);
                    return false;
                }

                if (usbBoot && installation.IsWindows2000 && installation.ServicePackVersion < 4)
                {
                    Console.WriteLine("Error: the /usbboot switch is only supported with Windows 2000 SP4.");
                    Console.WriteLine("Note that earlier versions of Windows 2000 do not support USB 2.0.");
                    return false;
                }
                return true;
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("IntegrateDRV v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Author: Tal Aloni (tal.aloni.il@gmail.com)");
            Console.WriteLine("About: This software is designed to integrate mass-storage drivers into windows");
            Console.WriteLine("       XP/2003 installation / temporary installation directories.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("------");
            Console.WriteLine("- IntegrateDRV [/textmodedriver=<path>] [/target=<path>]");
            Console.WriteLine("- IntegrateDRV [/driver=<path>] [/target=<path>]");
            Console.WriteLine("- IntegrateDRV [/driver=<path>] [/target=<path>] [/local [/preconf]]");
            Console.WriteLine("- IntegrateDRV [/driver=<path>] [/target=<path>] [/enum=<path> [/preconf]]");
            Console.WriteLine("- IntegrateDRV [/usbboot] [/target=<path>]");
            Console.WriteLine();
            Console.WriteLine("* If [/target] is not specified, C:\\ will be used");
            Console.WriteLine();
            Console.WriteLine("* The /local switch will only display hardware that is present locally.");
            Console.WriteLine("* The /enum switch will only display hardware that is present in a registry");
            Console.WriteLine("  export of HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Enum.");
            Console.WriteLine();
            Console.WriteLine("Note about integrating PNP Drivers for network adapters:");
            Console.WriteLine("--------------------------------------------------------");
            Console.WriteLine("Network adapters require special registry configuration to support boot start.");
            Console.WriteLine("This configuration can be set in one of 3 ways:");
            Console.WriteLine("1. Integrate a driver called NICBootConf to configure the NIC during each boot.");
            Console.WriteLine("2. Run this software from the target machine and use the /local and /preconf");
            Console.WriteLine("   switches, IntegrateDRV will pre-configure the network adapter.");
            Console.WriteLine("3. Export HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Enum from the target");
            Console.WriteLine("   machine, and provide the path to the exported key using the /enum and");
            Console.WriteLine("   /preconf switches.");
        }

        public static void Exit()
        {
            new SetupRegistryHiveFile().UnloadHive(false);
            Environment.Exit(-1);
        }

    }
}
