using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class NetGPCINFFile : ServiceINIFile
    {
        /// <summary>
        /// GPC stands for General Packet Classifier
        /// </summary>
        public NetGPCINFFile() : base("netgpc.inf")
        {}

        public void SetGPCToBootStart()
        {
            SetServiceToBootStart("GPC.AddService");
        }
    }
}
