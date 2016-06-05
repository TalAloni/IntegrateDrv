using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;

namespace Utilities
{
    /// <summary>
    /// This class does everything through Win32 API, because currently Mono does not implement RegistryKey.SetAccessControl()
    /// </summary>
    public class RegistryKeyUtils
    {
        public const string DesiredSecurityDescriptorString = "O:BAG:BAD:(A;OICI;GA;;;SY)(A;OICI;GA;;;BA)";
        //public const string DefaultEnumSecurityDescriptorString = "O:BAG:BAD:(A;OICI;GA;;;SY)(A;OICI;GR;;;WD)";
        // We must give administrators permission to \ControlSet001\Enum
        public const string DesiredEnumSecurityDescriptorString = "O:BAG:BAD:(A;OICI;GA;;;SY)(A;OICI;GR;;;WD)(A;OICI;GA;;;BA)"; // WD = Everyone, SY = SYSTEM, BA = Administrators

        [Flags]
        enum SecurityInformation : uint
        {
            Owner = 0x00000001,
            Group = 0x00000002,
            Dacl = 0x00000004,
            Sacl = 0x00000008,
            ProtectedDacl = 0x80000000,
            ProtectedSacl = 0x40000000,
            UnprotectedDacl = 0x20000000,
            UnprotectedSacl = 0x10000000
        }

        [StructLayout(LayoutKind.Sequential)]
        private class SECURITY_DESCRIPTOR
        {
            public byte revision;
            public byte size;
            public short control;
            public IntPtr owner;
            public IntPtr group;
            public IntPtr sacl;
            public IntPtr dacl;
        }

        [DllImport("Advapi32.dll")]
        private static extern int RegOpenKeyEx(uint hKey, string lpSubKey, uint ulOptions, int samDesired, out int phkResult);

        [DllImport("Advapi32.dll")]
        private static extern int RegCloseKey(int keyHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegGetKeySecurity(int keyHandle, SecurityInformation SecurityInformation, ref IntPtr pSecurityDescriptor, ref ulong lpcbSecurityDescriptor);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
          string StringSecurityDescriptor,
          int StringSDRevision,
            // NOTE: The unmanaged API allocates the memory for this
            // buffer and makes you responsible for cleaning it up!
          out IntPtr SecurityDescriptor,
          out int SecurityDescriptorSize
        ); 

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegSetKeySecurity(int keyHandle, SecurityInformation securityInformation, IntPtr pSecurityDescriptor);

        /*
        // This method will return the SECURITY_DESCRIPTOR structure of existing registry key
        private bool GetSecurityDescriptor(UIntPtr KeyEntry, out SECURITY_DESCRIPTOR descriptor)
        {
            descriptor = new SECURITY_DESCRIPTOR();
            int buffer = Marshal.SizeOf(typeof(SECURITY_DESCRIPTOR)) * 100; //to make the buffer large enough.
            int error;
            error = RegGetKeySecurity(KeyEntry, SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, ref descriptor, ref buffer);
            error = GetLastError();
            if (error != 0)
                return false;
            else
                return true;
        }*/

        /*
        public static byte[] ConvertStringSDToSD(string securityDescriptor)
        {
            IntPtr pSecurityDescriptor;
            int securityDescriptorLen;
            bool success = ConvertStringSecurityDescriptorToSecurityDescriptor(
              securityDescriptor, 1, out pSecurityDescriptor, out securityDescriptorLen
              );

            // The following ensures that the memory allocated to pSecurityDescriptor
            // by the unmanaged Win32 API is freed.
            try
            {
                if (!success)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                byte[] sd = new byte[securityDescriptorLen];
                Marshal.Copy(pSecurityDescriptor, sd, 0, securityDescriptorLen);

                return sd;
            }
            finally
            {
                if (pSecurityDescriptor != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pSecurityDescriptor);
                    pSecurityDescriptor = IntPtr.Zero;
                }
            }
        }
        */

        public static void GiveAdministratorsFullControl(string hKeyUsersSubKeyName)
        {
            // Administrators and SYSTEM are granted Full Control:
            SetHKeyUsersKeySecutiryDescriptor(hKeyUsersSubKeyName, DesiredSecurityDescriptorString);
        }

        // Security Descriptor String Format:
        // http://msdn.microsoft.com/en-us/library/aa379570%28v=vs.85%29.aspx
        // http://msdn.microsoft.com/en-us/magazine/cc982153.aspx
        public static void SetHKeyUsersKeySecutiryDescriptor(string subKeyName, string securityDescriptorString)
        {
            int keyHandle;
            int retVal = RegOpenKeyEx(RegistryUtils.HKEY_USERS, subKeyName, 0, (int)RegistryRights.ReadKey | (int)RegistryRights.ChangePermissions, out keyHandle);
            if (retVal != 0) //If the function succeeds, the return value is 0
            {
                Console.WriteLine("Error: Failed to open registry key.");
                Environment.Exit(-1);
            }

            IntPtr pSecurityDescriptor;
            int securityDescriptorSize;
            bool bRetVal = ConvertStringSecurityDescriptorToSecurityDescriptor(securityDescriptorString, 1, out pSecurityDescriptor, out securityDescriptorSize);
            if (!bRetVal)
            {
                Console.WriteLine("Error: Failed to obtain security descriptor.");
                Environment.Exit(-1);
            }

            retVal = RegSetKeySecurity(keyHandle, SecurityInformation.Dacl, pSecurityDescriptor);
            if (retVal != 0) //If the function succeeds, the return value is 0
            {
                Console.WriteLine("Error: Failed to set security descriptor for registry key.");
                Environment.Exit(-1);
            }

            if (pSecurityDescriptor != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pSecurityDescriptor);
                pSecurityDescriptor = IntPtr.Zero;
            }

            retVal = RegCloseKey(keyHandle);
            if (retVal != 0) //If the function succeeds, the return value is 0
            {
                Console.WriteLine("Warning: Failed to close registry key.");
            }
        }

        public static string GetShortKeyName(string fullKeyName)
        {
            int index = fullKeyName.LastIndexOf(@"\");
            if (index >= 0)
            {
                return fullKeyName.Substring(index + 1);
            }
            else
            {
                return fullKeyName;
            }
        }
    }
}
