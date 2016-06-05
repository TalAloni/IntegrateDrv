using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;
using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;

namespace IntegrateDrv
{
    public class KernelAndHalIntegrator
    {
        private WindowsInstallation m_installation;
        private static bool m_uniprocessorKernelEnabled = false;
        private static bool m_multiprocessorHalEnabled = false;

        public KernelAndHalIntegrator(WindowsInstallation installation)
        {
            m_installation = installation;
        }

        // The following combinations of kernel and HAL will enable NICs to work in text-mode setup:
        // (regardless of whether the machine being used has one or more than one CPU core)
        // 
        // Uniprocessor kernel   + ACPI PC HAL
        // Uniprocessor kernel   + ACPI Uniprocessor PC HAL
        // Multiprocessor kernel + ACPI Multiprocessor PC HAL
        //
        // Other combinations will either hang or reboot.
        // By default, text-mode setup will use Multiprocessor kernel + ACPI Uniprocessor PC HAL (which will hang)
        //
        // Note that we can use both multiprocessor kernel and multiprocessor HAL on uniprocessor machine,
        // (there might be a performance penalty for such configuration)
        // however, this approach will not work on older machines that uses the ACPI PC HAL (Pentium 3 / VirtualBox)
        // so the best approach is using the uniprocessor kernel.

        // references:
        // http://support.microsoft.com/kb/309283
        // http://social.technet.microsoft.com/Forums/en-AU/configmgrosd/thread/fb1dbea9-9d39-4663-9c61-6bcdb4c1253f

        // general information about x86 HAL types: http://www.vernalex.com/guides/sysprep/hal.shtml

        // Note: /kernel switch does not work in text-mode, so we can't use this simple solution.
        public void UseUniprocessorKernel()
        {
            if (m_uniprocessorKernelEnabled)
            {
                return;
            }

            if (m_installation.ArchitectureIdentifier != "x86")
            {
                // amd64 and presumably ia64 use a single HAL for both uni and multiprocessor kernel)
                return;
            }

            Console.WriteLine();
            Console.WriteLine("By default, text-mode setup will use a multiprocessor OS kernel with a");
            Console.WriteLine("uniprocessor HAL. This configuration cannot support network adapters");
            Console.WriteLine("(setup will hang).");
            Console.WriteLine("IntegrateDrv will try to enable uniprocessor kernel:");

            string setupldrPath;
            if (m_installation.IsTargetContainsTemporaryInstallation)
            {
                // sometimes NTLDR is being used instead of $LDR$ (when using winnt32.exe /syspart /tempdrive)
                // (even so, it's still a copy of setupldr.bin and not NTLDR from \I386)
                setupldrPath = m_installation.TargetDirectory + "$LDR$";
                if (!File.Exists(setupldrPath))
                {
                    setupldrPath = m_installation.TargetDirectory + "NTLDR";
                }
            }
            else // integration to installation media
            {
                setupldrPath = m_installation.SetupDirectory + "setupldr.bin";
            }

            if (!File.Exists(setupldrPath))
            {
                Console.WriteLine("Error: '{0}' does not exist.", setupldrPath);
                Program.Exit();
            }

            if (m_installation.IsWindows2000)
            {
                PatchWindows2000SetupBootLoader(setupldrPath);
            }
            else // Winndows XP or Windows Server 2003
            {
                PatchWindowsXP2003SetupBootLoader(setupldrPath);
            }

            if (m_installation.IsTargetContainsTemporaryInstallation)
            {
                ProgramUtils.CopyCriticalFile(m_installation.SetupDirectory + "ntoskrnl.ex_", m_installation.BootDirectory + "ntoskrnl.ex_");
            }
            else // integration to installation media
            {
                m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory("ntoskrnl.ex_");
            }
            Console.WriteLine("Uniprocessor kernel has been enabled.");

            m_uniprocessorKernelEnabled = true;
        }

        private void PatchWindows2000SetupBootLoader(string setupldrPath)
        {
            // Windows 2000 boot loader does not contain a portable executable or a checksum verification mechanism
            byte[] bytes = File.ReadAllBytes(setupldrPath);
            // update executable byte array
            byte[] oldSequence = Encoding.ASCII.GetBytes("ntkrnlmp.exe");
            byte[] newSequence = Encoding.ASCII.GetBytes("ntoskrnl.exe");
            ReplaceInBytes(ref bytes, oldSequence, newSequence);
            FileSystemUtils.ClearReadOnlyAttribute(setupldrPath);
            File.WriteAllBytes(setupldrPath, bytes);
        }

        private void PatchWindowsXP2003SetupBootLoader(string setupldrPath)
        {
            byte[] bytes = File.ReadAllBytes(setupldrPath);
            byte[] dosSignature = BitConverter.GetBytes(DosHeader.DosSignature);

            // setupldr.bin and ntldr are regular executables that are preceded by a special loader
            // we use the MZ DOS signature to determine where the executable start.
            // Note that we must update the executable checksum, because the loader will verify that the executable checksum is correct
            int executableOffset = IndexOfSequence(ref bytes, dosSignature);

            if (executableOffset == -1)
            {
                Console.WriteLine("Error: setupldr.bin is corrupted.");
                Program.Exit();
            }

            byte[] loader = new byte[executableOffset];
            Array.Copy(bytes, loader, executableOffset);

            int executableLength = bytes.Length - executableOffset;
            byte[] executable = new byte[executableLength];
            Array.Copy(bytes, executableOffset, executable, 0, executableLength);

            PortableExecutableInfo peInfo = new PortableExecutableInfo(executable);

            // update executable byte array
            byte[] oldSequence = Encoding.ASCII.GetBytes("ntkrnlmp.exe");
            byte[] newSequence = Encoding.ASCII.GetBytes("ntoskrnl.exe");
            // the kernel filename appears in the first PE section
            byte[] section = peInfo.Sections[0];
            ReplaceInBytes(ref section, oldSequence, newSequence);
            peInfo.Sections[0] = section;

            MemoryStream peStream = new MemoryStream();
            PortableExecutableInfo.WritePortableExecutable(peInfo, peStream);
            executable = peStream.ToArray();
            // finished updating executable byte array

            FileSystemUtils.ClearReadOnlyAttribute(setupldrPath);
            FileStream stream = new FileStream(setupldrPath, FileMode.Create, FileAccess.ReadWrite);
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(loader);
            writer.Write(executable);
            writer.Close();
        }

        /// <returns>true if replacement occured</returns>
        public static bool ReplaceInBytes(ref byte[] bytes, byte[] oldSequence, byte[] newSequence)
        {
            bool result = false;
            if (oldSequence.Length != newSequence.Length)
            {
                throw new ArgumentException("oldSequence must have the same length as newSequence");
            }

            int index = IndexOfSequence(ref bytes, oldSequence);
            while (index != -1)
            {
                result = true;
                for (int j = 0; j < newSequence.Length; j++)
                {
                    bytes[index + j] = newSequence[j];
                }
                index = IndexOfSequence(ref bytes, oldSequence);
            }
            return result;
        }

        private static int IndexOfSequence(ref byte[] bytes, byte[] sequence)
        {
            return IndexOfSequence(ref bytes, 0, sequence);
        }

        private static int IndexOfSequence(ref byte[] bytes, int startIndex, byte[] sequence)
        {
            for (int index = 0; index < bytes.Length - sequence.Length; index++)
            {
                bool match = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (bytes[index + j] != sequence[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return index;
                }
            }
            return -1;
        }

        // This approach does not work for older machines that uses the ACPI PC HAL (Pentium 3 / VirtualBox)
        [Obsolete]
        public void UseMultiprocessorHal()
        {
            if (m_multiprocessorHalEnabled)
            {
                return;
            }
            if (m_installation.ArchitectureIdentifier != "x86")
            {
                // amd64 and presumably ia64 use a single HAL for both uni and multiprocessor kernel)
                return;
            }

            Console.WriteLine();
            Console.WriteLine("By default, text-mode setup will use a multiprocessor OS kernel");
            Console.WriteLine("with a uniprocessor HAL. This configuration cannot support network adapters");
            Console.WriteLine("(setup will hang).");
            Console.WriteLine("IntegrateDrv will try to enable multiprocessor HAL:");

            if (m_installation.IsTargetContainsTemporaryInstallation)
            {
                if (System.IO.File.Exists(m_installation.BootDirectory + "halmacpi.dl_"))
                {
                    m_installation.TextSetupInf.UseMultiprocessorHal();
                    m_multiprocessorHalEnabled = true;
                    Console.WriteLine("Multiprocessor HAL has been enabled.");
                    return;
                }
                else if (System.IO.File.Exists(m_installation.SetupDirectory + "halmacpi.dl_"))
                {
                    ProgramUtils.CopyCriticalFile(m_installation.SetupDirectory + "halmacpi.dl_", m_installation.BootDirectory + "halmacpi.dl_");
                    m_installation.TextSetupInf.UseMultiprocessorHal();
                    Console.WriteLine("halmacpi.dl_ was copied from local source directory.");
                    m_multiprocessorHalEnabled = true;
                    Console.WriteLine("Multiprocessor HAL has been enabled.");
                }
                else
                {
                    int index;
                    for(index = 3; index >= 1; index--)
                    {
                        string spFilename = string.Format("sp{0}.cab", index);
                        if (File.Exists(m_installation.SetupDirectory + spFilename))
                        {
                            CabInfo cabInfo = new CabInfo(m_installation.SetupDirectory + spFilename);
                            if (cabInfo.GetFile("halmacpi.dll") != null)
                            {
                                cabInfo.UnpackFile("halmacpi.dll", m_installation.BootDirectory + "halmacpi.dll");
                                // setup is expecting a packed "halmacpi.dl_"
                                //cabInfo = new CabInfo(m_installation.BootDirectory + "halmacpi.dl_");
                                //Dictionary<string, string> files = new Dictionary<string, string>();
                                //files.Add("halmacpi.dll", "halmacpi.dll");
                                //cabInfo.PackFileSet(m_installation.BootDirectory, files);
                                Console.WriteLine("halmacpi.dl_ was extracted from local source directory.");
                                m_installation.TextSetupInf.UseMultiprocessorHal();
                                m_multiprocessorHalEnabled = true;
                                Console.WriteLine("Multiprocessor HAL has been enabled.");
                            }
                            break;
                        }
                    }

                    if (index == 0)
                    {
                        Console.WriteLine("Warning: could not locate halmacpi.dll, multiprocessor HAL has not been enabled!");
                    }
                }
            }
            else // integration to installation media
            {
                m_installation.TextSetupInf.UseMultiprocessorHal();
                m_installation.DosNetInf.InstructSetupToCopyFileFromSetupDirectoryToBootDirectory("halmacpi.dl_");
                m_multiprocessorHalEnabled = true;
                Console.WriteLine("Multiprocessor HAL has been enabled.");
                return;
            }
        }
    }
}
