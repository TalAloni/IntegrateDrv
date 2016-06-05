using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class UsbPortINFFile : ServiceINIFile
    {
        public UsbPortINFFile() : base("usbport.inf")
        {}

        public void SetUsbEnhancedHostControllerToBootStart()
        {
            SetServiceToBootStart("EHCI.AddService");
            SetServiceLoadOrderGroup("EHCI.AddService", "Boot Bus Extender");
        }

        public void SetUsbOpenHostControllerToBootStart()
        {
            SetServiceToBootStart("OHCI.AddService");
            SetServiceLoadOrderGroup("OHCI.AddService", "Boot Bus Extender");
        }

        public void SetUsbUniversalHostControllerToBootStart()
        {
            SetServiceToBootStart("UHCI.AddService");
            SetServiceLoadOrderGroup("UHCI.AddService", "Boot Bus Extender");
        }

        public void SetUsbRootHubToBootStart()
        {
            SetServiceToBootStart("ROOTHUB.AddService");
            SetServiceLoadOrderGroup("ROOTHUB.AddService", "Boot Bus Extender");
        }
    }
}
