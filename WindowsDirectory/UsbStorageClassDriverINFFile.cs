using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class UsbStorageClassDriverINFFile : ServiceINIFile
    {
        public UsbStorageClassDriverINFFile() : base("usbstor.inf")
        {}

        public void SetUsbStorageClassDriverToBootStart()
        {
            SetServiceToBootStart("USBSTOR.AddService");
            SetServiceLoadOrderGroup("USBSTOR.AddService", "Boot Bus Extender");
        }
    }
}
