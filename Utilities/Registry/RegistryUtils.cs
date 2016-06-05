using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Utilities
{
    public class RegistryUtils
    {
        public const uint HKEY_USERS = 0x80000003;

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int RegLoadKey(uint hKey, string lpSubKey, string lpFile);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int RegUnLoadKey(uint hKey, string lpSubKey);

        public static int LoadHive(string subkey, string hivePath)
        {
            SecurityUtils.ObtainBackupRestorePrivileges();
            
            int result = RegLoadKey(HKEY_USERS, subkey, hivePath);
            return result;
        }

        public static int UnloadHive(string subkey)
        {
            SecurityUtils.ObtainBackupRestorePrivileges();
            return RegUnLoadKey(HKEY_USERS, subkey);
        }
    }
}
