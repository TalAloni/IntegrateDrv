using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class DeviceService
    {
        private string m_deviceDescription = String.Empty;
        private string m_serviceName = String.Empty;
        private string m_serviceDisplayName = String.Empty;
        private string m_serviceGroup;
        private int m_serviceType;
        private int m_errorControl;
        private string m_fileName = String.Empty;
        private string m_imagePath = String.Empty;

        public DeviceService(string deviceDescription, string serviceName, string serviceDisplayName, string serviceGroup, int serviceType, int errorControl, string fileName, string imagePath)
        {
            m_deviceDescription = deviceDescription;
            m_serviceName = serviceName;
            m_serviceDisplayName = serviceDisplayName;
            m_serviceGroup = serviceGroup;
            m_serviceType = serviceType;
            m_errorControl = errorControl;
            m_fileName = fileName;
            m_imagePath = imagePath;
        }

        public string DeviceDescription
        {
            get
            {
                return m_deviceDescription;
            }
        }

        public string ServiceName
        {
            get
            {
                return m_serviceName;
            }
        }

        public string ServiceDisplayName
        {
            get
            {
                return m_serviceDisplayName;
            }
        }

        public string ServiceGroup
        {
            get
            {
                return m_serviceGroup;
            }
        }

        public int ServiceType
        {
            get
            {
                return m_serviceType;
            }
        }

        public int ErrorControl
        {
            get
            {
                return m_errorControl;
            }
        }
        /// <summary>
        /// File name of the service executable image (.sys file)
        /// </summary>
        public string FileName
        {
            get
            {
                return m_fileName;
            }
        }

        // this will only be used for GUI-mode
        public string ImagePath
        {
            get
            {
                return m_imagePath;
            }
        }

        // Text-mode setup will always initialize services based on the values stored under
        // Services\serviceName, where serviceName is the service file name without the .sys extension.
        // if serviceName != file name without the .sys extension, and we want the service to work properly in text mode,
        // we can either change the service name or change the filename

        public string TextModeFileName
        {
            get
            {
                return m_serviceName + ".sys";
            }
        }
    }
}
