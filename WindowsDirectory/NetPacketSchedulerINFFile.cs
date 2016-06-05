using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class NetPacketSchedulerINFFile : ServiceINIFile
    {
        public NetPacketSchedulerINFFile() : base("netpschd.inf")
        { 
        }

        public void SetPacketSchedulerToBootStart()
        {
            SetServiceToBootStart("PSched.AddService");
        }
    }
}
