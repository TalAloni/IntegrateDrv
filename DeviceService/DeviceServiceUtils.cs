using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class DeviceServiceUtils
    {
        public static List<NetworkDeviceService> FilterNetworkDeviceServices(List<DeviceService> deviceServices)
        {
            List<NetworkDeviceService> result = new List<NetworkDeviceService>();
            foreach (DeviceService deviceService in deviceServices)
            {
                if (deviceService is NetworkDeviceService)
                {
                    result.Add((NetworkDeviceService)deviceService);
                }
            }
            return result;
        }

        public static bool ContainsService(List<DeviceService> deviceServices, string serviceNameToFind)
        {
            foreach (DeviceService deviceService in deviceServices)
            {
                if (String.Equals(deviceService.ServiceName, serviceNameToFind, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
