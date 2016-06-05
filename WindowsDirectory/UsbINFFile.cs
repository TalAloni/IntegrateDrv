using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class UsbINFFile : ServiceINIFile
    {
        public UsbINFFile() : base("usb.inf")
        {}

        public void SetWindows2000OpenHostControllerToBootStart()
        {
            SetServiceToBootStart("OpenHCD.AddService");
            SetServiceLoadOrderGroup("OpenHCD.AddService", "Boot Bus Extender");
        }

        public void SetWindows2000UsbUniversalHostControllerToBootStart()
        {
            SetServiceToBootStart("UniversalHCD.AddService");
            SetServiceLoadOrderGroup("UniversalHCD.AddService", "Boot Bus Extender");
        }

        public void SetWindows2000UsbEnhancedHostControllerToBootStart()
        {
            SetServiceToBootStart("EHCI.AddService");
            SetServiceLoadOrderGroup("EHCI.AddService", "Boot Bus Extender");
        }

        /// <summary>
        /// USB 1.x Root Hub / Generic Hub
        /// </summary>
        public void SetWindows2000UsbRootHubToBootStart()
        {
            SetServiceToBootStart("StandardHub.AddService");
            SetServiceLoadOrderGroup("StandardHub.AddService", "Boot Bus Extender");
        }

        /// <summary>
        /// USB 2.0 Root Hub
        /// </summary>
        public void SetWindows2000Usb20RootHubToBootStart()
        {
            SetServiceToBootStart("ROOTHUB2.AddService");
            SetServiceLoadOrderGroup("ROOTHUB2.AddService", "Boot Bus Extender");
        }

        /// <summary>
        /// USB 2.0 Generic Hub
        /// </summary>
        public void SetWindows2000Usb20GenericHubToBootStart()
        {
            SetServiceToBootStart("Usb2Hub.AddService");
            SetServiceLoadOrderGroup("Usb2Hub.AddService", "Boot Bus Extender");
        }

        public void SetWindowsXP2003UsbStandardHubToBootStart()
        {
            SetServiceToBootStart("StandardHub.AddService");
            SetServiceLoadOrderGroup("StandardHub.AddService", "Boot Bus Extender");
        }

        /// <summary>
        /// AFAIK not needed for booting from a UFD
        /// </summary>
        [Obsolete]
        public void SetWindowsXP2003UsbGenericParentDriverToBootStart()
        {
            SetServiceToBootStart("CommonClassParent.AddService");
            SetServiceLoadOrderGroup("CommonClassParent.AddService", "Boot Bus Extender");
        }
    }
}
