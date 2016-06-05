using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class TextModeDriverDirectory
    {
        private string m_path = String.Empty;
        private TextModeDriverSetupINIFile m_descriptor;
        
        public TextModeDriverDirectory(string path)
        {
            m_path = path;
            if (this.ContainsOEMSetupFile)
            {
                m_descriptor = new TextModeDriverSetupINIFile();
                m_descriptor.ReadFromDirectory(path);
            }
        }

        public bool ContainsOEMSetupFile
        {
            get
            { 
                return FileSystemUtils.IsFileExist(m_path + "txtsetup.oem");
            }
        }

        public TextModeDriverSetupINIFile TextModeDriverSetupINI
        {
            get
            {
                return m_descriptor;
            }
        }

        public string Path
        {
            get
            {
                return m_path;
            }
        }
    }
}
