using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class NetTCPIPINFFile : ServiceINIFile
    {
        public NetTCPIPINFFile() : base("nettcpip.inf")
        {
        }

        public void SetTCPIPToBootStart()
        {
            SetServiceToBootStart("Install.AddService.TCPIP");
        }

        public void SetIPSecToBootStart()
        {
            SetServiceToBootStart("Install.AddService.IPSEC");
        }
    }
}
