using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class NetworkDeviceService : DeviceService
    {
        private string m_netCfgInstanceID = String.Empty;

        public NetworkDeviceService(string deviceDescription, string serviceName, string serviceDisplayName, string serviceGroup, int serviceType, int errorControl, string fileName, string imagePath, string netCfgInstanceID)
            : base(deviceDescription, serviceName, serviceDisplayName, serviceGroup, serviceType, errorControl, fileName, imagePath)
        {
            m_netCfgInstanceID = netCfgInstanceID;
        }

        public string NetCfgInstanceID
        {
            get
            {
                return m_netCfgInstanceID;
            }
        }
    }
}
