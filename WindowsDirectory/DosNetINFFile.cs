using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrateDrv
{
    public class DosNetINFFile : INIFile
    {
        private const string SetupDirectoryID = "d1";

        public DosNetINFFile() : base("dosnet.inf")
        { 
        }

        /// <summary>
        /// Necessary for files to be copied
        /// </summary>
        /// <param name="sourceDirectoryPath">
        /// In the form: 'SCSIDRV' or 'SCSIDRV\AMDAHCI'
        /// </param>
        public void AddOptionalSourceDirectory(string sourceDirectoryPath)
        {
            if (!SectionNames.Contains("OptionalSrcDirs"))
            {
                AddSection("OptionalSrcDirs");
            }
            if (GetLineIndex("OptionalSrcDirs", sourceDirectoryPath) == -1)
            {
                AppendLineToSection("OptionalSrcDirs", sourceDirectoryPath);
            }
        }

        private void AddSourceDirectory(string directoryID, string sourceDirectoryPathInMediaRootForm)
        {
            string line = String.Format("{0} = {1}", directoryID, sourceDirectoryPathInMediaRootForm);
            AppendLineToSection("Directories", line);

            if (sourceDirectoryPathInMediaRootForm.ToLower().StartsWith("\\i386\\"))
            {
                AddOptionalSourceDirectory(sourceDirectoryPathInMediaRootForm.Substring(6));
            }
            else if (sourceDirectoryPathInMediaRootForm.ToLower().StartsWith("\\amd64\\"))
            {
                AddOptionalSourceDirectory(sourceDirectoryPathInMediaRootForm.Substring(7));
            }
            else if (sourceDirectoryPathInMediaRootForm.ToLower().StartsWith("\\ia64\\"))
            {
                AddOptionalSourceDirectory(sourceDirectoryPathInMediaRootForm.Substring(6));
            }
        }

        private string GetSourceDirectoryID(string sourceDirectoryPathInMediaRootForm)
        {
            List<string> section = GetSection("Directories");
            foreach (string line in section)
            {
                KeyValuePair<string, List<string>> keyAndValues = GetKeyAndValues(line);
                string path = keyAndValues.Value[0];
                if (path == sourceDirectoryPathInMediaRootForm)
                {
                    return keyAndValues.Key;
                }
            }
            return String.Empty;
        }

        private string AllocateSourceDirectoryID(string sourceDirectoryPathInMediaRootForm)
        {
            string sourceDirectoryID = GetSourceDirectoryID(sourceDirectoryPathInMediaRootForm);
            if (sourceDirectoryID == String.Empty)
            {
                int index = 11;
                while (IsDirectoryIDTaken("d" + index.ToString()))
                {
                    index++;
                }
                sourceDirectoryID = "d" + index.ToString();
                AddSourceDirectory(sourceDirectoryID, sourceDirectoryPathInMediaRootForm);
            }
            return sourceDirectoryID;
        }

        public void InstructSetupToCopyFileFromSetupDirectoryToLocalSourceDriverDirectory(string sourceDirectoryInMediaRootForm, string fileName)
        {
            string sourceDirectoryID = AllocateSourceDirectoryID(sourceDirectoryInMediaRootForm);

            string line = String.Format("{0},{1}", sourceDirectoryID, fileName);
            if (GetLineIndex("Files", line) == -1)
            {
                AppendLineToSection("Files", line);
            }
        }

        public void InstructSetupToCopyFileFromSetupDirectoryToLocalSourceRootDirectory(string fileName)
        {
            InstructSetupToCopyFileFromSetupDirectoryToLocalSourceRootDirectory(String.Empty, fileName);
        }

        public void InstructSetupToCopyFileFromSetupDirectoryToLocalSourceRootDirectory(string sourceFilePath, string fileName)
        {
            string line = String.Format("{0},{1}", SetupDirectoryID, fileName);
            if (sourceFilePath != String.Empty)
            {
                line += "," + sourceFilePath;
            }
            if (GetLineIndex("Files", line) == -1)
            {
                AppendLineToSection("Files", line);
            }
        }

        public void InstructSetupToCopyFileFromSetupDirectoryToBootDirectory(string fileName)
        {
            InstructSetupToCopyFileFromSetupDirectoryToBootDirectory(String.Empty, fileName);
        }

        public void InstructSetupToCopyFileFromSetupDirectoryToBootDirectory(string sourceFilePath, string fileName)
        {
            string line = String.Format("{0},{1}", SetupDirectoryID, fileName);
            if (sourceFilePath != String.Empty)
            {
                line += "," + sourceFilePath;
            }
            if (GetLineIndex("FloppyFiles.1", line) == -1)
            {
                AppendLineToSection("FloppyFiles.1", line);
            }
        }

        private bool IsDirectoryIDTaken(string directoryID)
        {
            List<string> section = GetSection("Directories");
            foreach (string line in section)
            {
                KeyValuePair<string, List<string>> keyAndValues = INIFile.GetKeyAndValues(line);
                if (keyAndValues.Key.Equals(directoryID))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
