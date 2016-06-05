using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class PNPDriverINFFile : ServiceINIFile
    {
        public const string NetworkAdapterClassName = "Net";
        public const string NetworkAdapterClassGUID = "{4D36E972-E325-11CE-BFC1-08002BE10318}";

        private string m_className = null;
        private string m_classGUID = null;
        private string m_provider = null;
        private string m_catalogFile = null;
        private string m_driverVersion = null;
        
        List<KeyValuePair<string, string>> m_devices = null;

        public PNPDriverINFFile(string fileName) : base(fileName)
        {
        }

        public void SetServiceToBootStart(string installSectionName, string architectureIdentifier, int minorOSVersion)
        {
            SetServiceToBootStart(installSectionName, architectureIdentifier, minorOSVersion, false);
        }

        public void SetServiceToBootStart(string installSectionName, string architectureIdentifier, int minorOSVersion, bool updateConsole)
        {
            List<string> installServicesSection = GetInstallServicesSection(installSectionName, architectureIdentifier, minorOSVersion);
            foreach (string line in installServicesSection)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                if (keyAndValues.Key == "AddService")
                {
                    string serviceName = keyAndValues.Value[0];
                    string serviceInstallSection = keyAndValues.Value[2];
                    this.SetServiceToBootStart(serviceInstallSection);
                    if (updateConsole)
                    {
                        Console.WriteLine("Service '" + serviceName + "' has been set to boot start");
                    }
                }
            }
        }

        // str can be either be a token or not
        public string ExpandToken(string str)
        {
            int leftIndex = str.IndexOf('%');
            if (leftIndex == 0)
            {
                int rightIndex = str.IndexOf('%', 1);
                if (rightIndex >= 0 && rightIndex == str.Length - 1)
                {
                    string token = str.Substring(leftIndex + 1, rightIndex - leftIndex - 1);
                    string tokenValue = GetTokenValue(token);
                    return tokenValue;
                }
            }
            return str;
        }

        public string GetTokenValue(string token)
        {
            List<string> strings = GetSection("Strings");
            foreach (string line in strings)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                if (keyAndValues.Key.Equals(token, StringComparison.InvariantCultureIgnoreCase))
                {
                    return keyAndValues.Value[0];
                }
            }
            throw new KeyNotFoundException(String.Format("Inf file '{0}' is not valid, token '{1}' was not found!", this.FileName, token));
        }

        public string ExpandDirID(string str)
        {
            int leftIndex = str.IndexOf('%');
            if (leftIndex >= 0)
            {
                int rightIndex = str.IndexOf('%', leftIndex + 1);
                if (rightIndex >= 0)
                {
                    string token = str.Substring(leftIndex + 1, rightIndex - leftIndex - 1);
                    string tokenValue = GetDirIDValue(token);
                    str = str.Substring(0, leftIndex) + tokenValue + str.Substring(rightIndex + 1);
                }
            }
            return str;
        }

        public string GetDirIDValue(string token)
        {
            if (token == "11")
            {
                return "system32";
            }
            else if (token == "12")
            {
                return @"system32\drivers";
            }
            throw new Exception("Inf file is not valid, dir-id not found!");
        }

        public List<string> ListManufacturerIDs()
        {
            List<string> manufacturerIDs = new List<string>();

            List<string> manufacturers = GetSection("Manufacturer");
            foreach (string manufacturer in manufacturers)
            {
                KeyValuePair<string, List<string>> manufacturerKeyAndValues = INIFile.GetKeyAndValues(manufacturer);
                if (manufacturerKeyAndValues.Value.Count >= 1)
                {
                    string manufacturerID = manufacturerKeyAndValues.Value[0];
                    manufacturerIDs.Add(manufacturerID);
                }
            }
            return manufacturerIDs;
        }

        public string GetDeviceInstallSectionName(string hardwareIDToFind, string architectureIdentifier, int minorOSVersion, int productType)
        {
            List<string> manufacturerIDs = ListManufacturerIDs();
            foreach (string manufacturerID in manufacturerIDs)
            {
                List<string> models = GetModelsSection(manufacturerID, architectureIdentifier, minorOSVersion, productType);
                foreach (string model in models)
                {
                    KeyValuePair<string, List<string>> modelKeyAndValues = INIFile.GetKeyAndValues(model);
                    if (modelKeyAndValues.Value.Count >= 2)
                    {
                        string hardwareID = modelKeyAndValues.Value[1];
                        if (String.Equals(hardwareID, hardwareIDToFind, StringComparison.InvariantCultureIgnoreCase))
                        { 
                            string installSectionName = modelKeyAndValues.Value[0];
                            return installSectionName;
                        }
                    }
                }
            }
            return String.Empty;
        }

        [Obsolete]
        public string FindMatchingHardwareID(string hardwareIDToFind, string architectureIdentifier, int minorOSVersion, int productType)
        {
            string genericHardwareID = GetGenericHardwareID(hardwareIDToFind);

            string matchingHardwareID = String.Empty;

            List<KeyValuePair<string, string>> devices = ListDevices(architectureIdentifier, minorOSVersion, productType);
            foreach (KeyValuePair<string, string> device in devices)
            {
                string hardwareID = device.Key;
                if (hardwareID.StartsWith(genericHardwareID, StringComparison.InvariantCultureIgnoreCase))
                {
                    matchingHardwareID = hardwareID;
                    break;
                }
            }
            return matchingHardwareID;
        }

        /// <summary>
        /// KeyValuePair contains HardwareID, DeviceName
        /// </summary>
        public List<KeyValuePair<string, string>> ListDevices(string architectureIdentifier, int minorOSVersion, int productType)
        {
            if (m_devices == null)
            {
                m_devices = new List<KeyValuePair<string, string>>();

                List<string> manufacturerIDs = ListManufacturerIDs();
                
                foreach (string manufacturerID in manufacturerIDs)
                {
                    List<string> models = GetModelsSection(manufacturerID, architectureIdentifier, minorOSVersion, productType);
                    foreach (string model in models)
                    {
                        KeyValuePair<string, List<string>> modelKeyAndValues = GetKeyAndValues(model);
                        if (modelKeyAndValues.Value.Count >= 2)
                        {
                            string deviceName;
                            try
                            {
                                // in XP x86 SP3, scsi.inf has a missing token,
                                // let's ignore the device in such case
                                deviceName = Unquote(ExpandToken(modelKeyAndValues.Key));
                            }
                            catch (KeyNotFoundException ex)
                            {
                                Console.WriteLine(ex.Message);
                                continue;
                            }
                            string hardwareID = modelKeyAndValues.Value[1];
                            m_devices.Add(new KeyValuePair<string, string>(hardwareID, deviceName));
                        }
                    }
                }
            }
            return m_devices;
        }

        public bool ContainsRootDevices(string architectureIdentifier, int minorOSVersion, int productType)
        {
            List<KeyValuePair<string, string>> devices = ListDevices(architectureIdentifier, minorOSVersion, productType);
            foreach(KeyValuePair<string, string> device in devices)
            {
                string hardwareID = device.Key;
                if (IsRootDevice(hardwareID))
                {
                    return true;
                }
            }
            return false;
        }

        public string GetDeviceManufacturerName(string hardwareIDToFind, string architectureIdentifier, int minorOSVersion, int productType)
        {
            List<string> manufacturers = GetSection("Manufacturer");
            foreach (string manufacturer in manufacturers)
            {
                KeyValuePair<string, List<string>> manufacturerKeyAndValues = GetKeyAndValues(manufacturer);
                if (manufacturerKeyAndValues.Value.Count >= 1)
                {
                    string manufacturerName = Unquote(ExpandToken(manufacturerKeyAndValues.Key));
                    string manufacturerID = manufacturerKeyAndValues.Value[0];

                    List<string> models = GetModelsSection(manufacturerID, architectureIdentifier, minorOSVersion, productType);
                    foreach (string model in models)
                    {
                        KeyValuePair<string, List<string>> modelKeyAndValues = GetKeyAndValues(model);
                        if (modelKeyAndValues.Value.Count >= 2)
                        {
                            string deviceName = Unquote(ExpandToken(modelKeyAndValues.Key));
                            string hardwareID = modelKeyAndValues.Value[1];
                            if (hardwareID.Equals(hardwareIDToFind)) // both hardwareIDs comes from the .inf so they must use the same case
                            {
                                return manufacturerName;
                            }
                        }
                    }
                }
            }
            return String.Empty;
        }


        public string GetDeviceDescription(string hardwareID, string architectureIdentifier, int minorOSVersion, int productType)
        {
            foreach (KeyValuePair<string, string> device in this.ListDevices(architectureIdentifier, minorOSVersion, productType))
            {
                if (device.Key.Equals(hardwareID))
                {
                    return device.Value;
                }
            }
            return String.Empty;
        }

        /// <summary>
        /// Sorted by priority
        /// </summary>
        public List<string> GetModelsSectionNames(string manufacturerID, string architectureIdentifier, int minorOSVersion, int productType)
        {
            // INF File Platform Extensions and x86-Based Systems:
            // http://msdn.microsoft.com/en-us/library/ff547425%28v=vs.85%29.aspx
            //
            // INF File Platform Extensions and x64-Based Systems
            // http://msdn.microsoft.com/en-us/library/ff547417%28v=vs.85%29.aspx

            // http://msdn.microsoft.com/en-us/library/ff547454%28v=vs.85%29.aspx
            // If the INF contains INF Models sections for several major or minor operating system version numbers,
            // Windows uses the section with the highest version numbers that are not higher than the operating
            // system version on which the installation is taking place. 

            // http://msdn.microsoft.com/en-us/library/ff539924%28v=vs.85%29.aspx
            // TargetOSVersion decoration format:
            // nt[Architecture][.[OSMajorVersion][.[OSMinorVersion][.[ProductType][.SuiteMask]]]]

            List<string> result = new List<string>();
            // Windows 2000 does not support platform extensions on an INF Models section name
            if (minorOSVersion != 0)
            {
                int minor = minorOSVersion;
                while (minor >= 0)
                {
                    string sectionName = String.Format("{0}.nt{1}.5", manufacturerID, architectureIdentifier);
                    // Even though Windows 2000 does not support platform extensions on models section name, Windows XP / Server 2003 can still use [xxxx.NTx86.5]
                    if (minor != 0)
                    {
                        sectionName += "." + minor;
                        result.Add(sectionName + "." + productType);
                    }
                    result.Add(sectionName);
                    minor--;
                }
                
                result.Add(manufacturerID + ".nt" + architectureIdentifier);
                // Starting from Windows Server 2003 SP1, only x86 bases systems can use the .nt platform extension / no platform extension on the Models section,
                // There is no point in supporting the not-recommended .nt platform extension / no platform extension for non-x86 drivers,
                // because surely an updated driver that uses the recommended platform extension exist (such driver will work for both Pre-SP1 and SP1+)
                if (architectureIdentifier == "x86")
                {
                    minor = minorOSVersion;
                    while (minor >= 0)
                    {
                        string sectionName = String.Format("{0}.nt.5", manufacturerID);
                        if (minor != 0)
                        {
                            sectionName += "." + minor;
                            result.Add(sectionName + "." + productType);
                        }
                        result.Add(sectionName);
                        minor--;
                    }

                    result.Add(manufacturerID + ".nt");
                }
            }
            result.Add(manufacturerID);

            return result;
        }

        public string GetMatchingModelsSectionName(string manufacturerID, string architectureIdentifier, int minorOSVersion, int productType)
        {
            List<string> modelsSectionNames = GetModelsSectionNames(manufacturerID, architectureIdentifier, minorOSVersion, productType);

            foreach (string modelsSectionName in modelsSectionNames)
            {
                if (StringUtils.ContainsCaseInsensitive(this.SectionNames, modelsSectionName))
                {
                    return modelsSectionName;
                }
            }

            return String.Empty;
        }

        /// <param name="minorOSVersion">We know that the major OS version is 5. XP x64, Server 2003 are 5.2, XP x86 is 5.1, Windows 2000 is 5.0</param>
        public List<string> GetModelsSection(string manufacturerID, string architectureIdentifier, int minorOSVersion, int productType)
        {
            string modelsSectionName = GetMatchingModelsSectionName(manufacturerID, architectureIdentifier, minorOSVersion, productType);
            if (modelsSectionName != String.Empty)
            {
                return GetSection(modelsSectionName);
            }
            else
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Sorted by priority
        /// </summary>
        public List<string> GetInstallSectionNames(string installSectionName, string architectureIdentifier, int minorOSVersion)
        {
            // http://msdn.microsoft.com/en-us/library/ff547344%28v=vs.85%29.aspx
            List<string> result = new List<string>();
            while (minorOSVersion >= 0)
            {
                string sectionName = String.Format("{0}.nt{1}.5", installSectionName, architectureIdentifier);
                if (minorOSVersion != 0)
                {
                    sectionName += "." + minorOSVersion;
                }
                result.Add(sectionName);
                minorOSVersion--;
            }
            
            result.Add(installSectionName + ".nt" + architectureIdentifier);
            result.Add(installSectionName + ".nt");
            result.Add(installSectionName);
            return result;
        }

        public string GetMatchingInstallSectionName(string installSectionName, string architectureIdentifier, int minorOSVersion)
        {
            List<string> installSectionNames = GetInstallSectionNames(installSectionName, architectureIdentifier, minorOSVersion);
            foreach (string sectionName in installSectionNames)
            {
                if (StringUtils.ContainsCaseInsensitive(this.SectionNames, sectionName))
                {
                    return sectionName;
                }
            }
            return String.Empty;
        }

        public List<string> GetInstallSection(string installSectionName, string architectureIdentifier, int minorOSVersion)
        {
            string matchingInstallSectionName = GetMatchingInstallSectionName(installSectionName, architectureIdentifier, minorOSVersion);
            if (matchingInstallSectionName != String.Empty)
            {
                return GetSection(matchingInstallSectionName);
            }
            else
            {
                return new List<string>();
            }
        }

        public List<string> GetInstallServicesSection(string installSectionName, string architectureIdentifier, int minorOSVersion)
        {
            List<string> installSectionNames = GetInstallSectionNames(installSectionName, architectureIdentifier, minorOSVersion);
            foreach (string sectionName in installSectionNames)
            {
                if (StringUtils.ContainsCaseInsensitive(this.SectionNames, sectionName + ".Services"))
                {
                    return GetSection(sectionName + ".Services");
                }
            }

            return new List<string>();
        }

        public bool DisableMatchingHardwareID(string hardwareIDToDisable, string architectureIdentifier, int minorOSVersion, int productType)
        {
            bool found = false;
            string genericHardwareID = GetGenericHardwareID(hardwareIDToDisable);

            List<string> manufacturerIDs = ListManufacturerIDs();
            foreach (string manufacturerID in manufacturerIDs)
            {
                string modelsSectionName = GetMatchingModelsSectionName(manufacturerID, architectureIdentifier, minorOSVersion, productType);
                if (modelsSectionName == String.Empty)
                {
                    continue;
                }
                List<string> models = GetSection(modelsSectionName);
                
                foreach (string model in models)
                {
                    KeyValuePair<string, List<string>> modelKeyAndValues = INIFile.GetKeyAndValues(model);
                    if (modelKeyAndValues.Value.Count >= 2)
                    {
                        string hardwareID = modelKeyAndValues.Value[1];

                        if (hardwareID.StartsWith(genericHardwareID, StringComparison.InvariantCultureIgnoreCase))
                        {
                            int lineIndex = GetLineIndex(modelsSectionName, model);
                            UpdateLine(lineIndex, ";" + model);
                            
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// This method will invalidate existing .inf digital signature (that is stored in a .cat file)
        /// </summary>
        [Obsolete]
        public void UnsignInf()
        {
            string comment = ";Added by IntegrateDrv for the purpose of unsigning the driver";
            if (!this.Text.StartsWith(comment))
            {
                InsertLine(0, comment);
            }
        }

        public string ClassName
        {
            get
            {
                if (m_className == null)
                {
                    List<string> values = GetValuesOfKeyInSection("Version", "Class");
                    if (values.Count >= 1)
                    {
                        m_className = values[0];
                    }
                    else
                    {
                        m_className = String.Empty;
                    }
                }
                return m_className;
            }
        }

        public string ClassGUID
        {
            get
            {
                if (m_classGUID == null)
                {
                    List<string> values = GetValuesOfKeyInSection("Version", "ClassGUID");
                    if (values.Count >= 1)
                    {
                        m_classGUID = values[0].ToUpper();
                    }
                    else
                    {
                        m_classGUID = String.Empty;
                    }
                }
                return m_classGUID;
            }
        }

        public string Provider
        {
            get
            {
                if (m_provider == null)
                {
                    List<string> values = GetValuesOfKeyInSection("Version", "Provider");
                    if (values.Count >= 1)
                    {
                        m_provider = Unquote(ExpandToken(values[0]));
                    }
                    else
                    {
                        m_provider = String.Empty;
                    }
                }
                return m_provider;
            }
        }

        public string CatalogFile
        {
            get
            {
                if (m_catalogFile == null)
                {
                    List<string> values = GetValuesOfKeyInSection("Version", "CatalogFile");
                    if (values.Count >= 1)
                    {
                        m_catalogFile = values[0];
                    }
                    else
                    {
                        m_catalogFile = String.Empty;
                    }
                }
                return m_catalogFile;
            }
        }

        public string DriverVersion
        {
            get
            {
                if (m_driverVersion == null)
                {
                    List<string> values = GetValuesOfKeyInSection("Version", "DriverVer");
                    // DriverVer=mm/dd/yyyy[,w.x.y.z]
                    if (values.Count >= 2)
                    {
                        m_driverVersion = values[1];
                    }
                    else
                    {
                        m_driverVersion = String.Empty;
                    }
                }
                return m_driverVersion;
            }
        }

        public static RegistryValueKind GetRegistryValueKind(string hexStringValueTypeflags)
        {
            int flags = ConvertFromIntStringOrHexString(hexStringValueTypeflags);
            return GetRegistryValueKind(flags);
        }

        public static RegistryValueKind GetRegistryValueKind(int flags)
        {
            int legalValues = 0x00000001 | 0x00010000 | 0x00010001 | 0x00020000;
            int value = flags & legalValues;
            switch (value)
            {
                case 0x00000000:
                    return RegistryValueKind.String;
                case 0x00000001:
                    return RegistryValueKind.Binary;
                case 0x00010000:
                    return RegistryValueKind.MultiString;
                case 0x00010001:
                    return RegistryValueKind.DWord;
                case 0x00020000:
                    return RegistryValueKind.ExpandString;
                default:
                    return RegistryValueKind.Unknown;
            }
        }

        public static int ConvertFromIntStringOrHexString(string value)
        {
            if (value.StartsWith("0x"))
            {
                return Int32.Parse(value.Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
            }
            else
            {
                return Conversion.ToInt32(value);
            }
        }

        public bool IsNetworkAdapter
        {
            get
            {
                return (String.Equals(this.ClassName, NetworkAdapterClassName, StringComparison.InvariantCultureIgnoreCase) ||
                        this.ClassGUID == NetworkAdapterClassGUID);
            }
        }

        // To the best of my limited knowledge all root devices are virtual except ACPI_HAL and such,
        // but there are many virtual devices that are not root devices (e.g. use the node ID of the PC's host controller)
        public static bool IsRootDevice(string hardwareID)
        {
            return hardwareID.ToLower().StartsWith(@"root\");
        }

        /// <summary>
        /// This method will remove the &SUBSYS and &REV entries from hardwareID
        /// </summary>
        public static string GetGenericHardwareID(string hardwareID)
        {
            string genericHardwareID = hardwareID;
            int subsysIndex = hardwareID.ToUpper().IndexOf("&SUBSYS");
            if (subsysIndex >= 0)
            {
                genericHardwareID = hardwareID.Substring(0, subsysIndex);
            }

            // sometimes &REV appears without &SUBSYS
            int revIndex = hardwareID.ToUpper().IndexOf("&REV");
            if (revIndex >= 0)
            {
                genericHardwareID = hardwareID.Substring(0, revIndex);
            }
            return genericHardwareID;
        }
    }
}
