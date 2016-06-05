using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Utilities;

namespace IntegrateDrv
{
    public class ExportedRegistryKey
    {
        private string m_keyName = String.Empty; // full key name
        private ExportedRegistry m_registry;

        public ExportedRegistryKey(ExportedRegistry registry, string keyName)
        {
            m_registry = registry;
            m_keyName = keyName;
        }

        public ExportedRegistryKey OpenSubKey(string subKeyName)
        {
            return new ExportedRegistryKey(m_registry, m_keyName + @"\" + subKeyName);
        }

        public object GetValue(string name)
        {
            return GetValue(name, null);
        }

        public object GetValue(string name, object defaultValue)
        {
            object result = m_registry.GetValue(m_keyName, name);
            if (result == null)
            {
                result = defaultValue;
            }
            return result;
        }

        public string[] GetSubKeyNames()
        {
            List<string> result = new List<string>();
            foreach (string sectionName in m_registry.SectionNames)
            { 
                if (sectionName.StartsWith(m_keyName + @"\", StringComparison.InvariantCultureIgnoreCase))
                {
                    string subKeyName = sectionName.Substring(m_keyName.Length + 1).Split('\\')[0];
                    if (!result.Contains(subKeyName))
                    {
                        result.Add(subKeyName);
                    }
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Full key name
        /// </summary>
        public string Name
        {
            get
            {
                return m_keyName;
            }
        }
    }
}
