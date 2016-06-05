using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public partial class TextSetupINFFile : INIFile
    {
        public TextSetupINFFile() : base("txtsetup.sif")
        {
        }

        public bool IsSourceDiskIDTaken(string architectureIdentifier, string sourceDiskID)
        {
            List<string> section = GetSection(String.Format("SourceDisksNames.{0}", architectureIdentifier));
            foreach(string line in section)
            {
                string key = GetKey(line);
                if (key == sourceDiskID)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsWinntDirectoryIDTaken(string winntDirectoryID)
        {
            List<string> section = GetSection("WinntDirectories");
            foreach(string line in section)
            {
                string key = GetKey(line);
                if (key == winntDirectoryID)
                {
                    return true;
                }
            }
            return false;
        }

        /// <returns>-1 if entry was not found</returns>
        public int GetSourceDiskID(string architectureIdentifier, string sourceDirectoryPathInMediaRootForm)
        {
            List<string> section = GetSection(String.Format("SourceDisksNames.{0}", architectureIdentifier));
            foreach (string line in section)
            { 
                KeyValuePair<string, List<string>> keyAndValues = GetKeyAndValues(line);
                string path = TryGetValue(keyAndValues.Value, 3);
                if (sourceDirectoryPathInMediaRootForm.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    return Convert.ToInt32(keyAndValues.Key);
                }
            }
            return -1;
        }

        public int AllocateSourceDiskID(string architectureIdentifier, string sourceDirectoryPathInMediaRootForm)
        {
            int sourceDiskID = GetSourceDiskID(architectureIdentifier, sourceDirectoryPathInMediaRootForm);
            if (sourceDiskID == -1)
            {
                sourceDiskID = 2001;
                while (IsSourceDiskIDTaken(architectureIdentifier.ToLower(), sourceDiskID.ToString()))
                {
                    sourceDiskID++;
                }
                AddSourceDiskEntry(architectureIdentifier, sourceDiskID.ToString(), sourceDirectoryPathInMediaRootForm);
            }
            return sourceDiskID;
        }

        private void AddSourceDiskEntry(string architectureIdentifier, string sourceDiskID, string sourceDirectoryPathInMediaRootForm)
        {
            string sectionName = String.Format("SourceDisksNames.{0}", architectureIdentifier);
            List<string> values = GetValuesOfKeyInSection(sectionName, "1");

            // use the values from the first entry. it's not straight forward to determine the cdtagfile for Windows 2000 (each edition has a different tagfile)
            string cdname = TryGetValue(values, 0);
            string cdtagfile = TryGetValue(values, 1);

            string line = String.Format("{0} = {1},{2},,{3}", sourceDiskID, cdname, cdtagfile, sourceDirectoryPathInMediaRootForm);
            AppendLineToSection(sectionName, line);
        }

        public int GetWinntDirectoryID(string winntDirectoryPath)
        {
            List<string> section = GetSection("WinntDirectories");
            foreach (string line in section)
            {
                KeyValuePair<string, List<string>> keyAndValues = GetKeyAndValues(line);
                string path = keyAndValues.Value[0];
                if (winntDirectoryPath.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    return Convert.ToInt32(keyAndValues.Key);
                }
            }
            return -1;
        }

        public int AllocateWinntDirectoryID(string winntDirectoryPath)
        {
            int winntDirectoryID = GetWinntDirectoryID(winntDirectoryPath);
            if (winntDirectoryID == -1)
            {
                winntDirectoryID = 3001;
                while (IsWinntDirectoryIDTaken(winntDirectoryID.ToString()))
                {
                    winntDirectoryID++;
                }
                AddWinntDirectory(winntDirectoryID.ToString(), winntDirectoryPath);
            }
            return winntDirectoryID;
        }

        private void AddWinntDirectory(string winntDirectoryID, string winntDirectoryPath)
        {
            string line = String.Format("{0} = {1}", winntDirectoryID, winntDirectoryPath);
            AppendLineToSection("WinntDirectories", line);
        }

        public void SetSourceDisksFileDriverEntry(string architectureIdentifier, string fileName, FileCopyDisposition fileCopyDisposition)
        {
            SetSourceDisksFileDriverEntry(architectureIdentifier, fileName, fileCopyDisposition, String.Empty);
        }

        public void SetSourceDisksFileDriverEntry(string architectureIdentifier, string fileName, FileCopyDisposition fileCopyDisposition, string destinationFileName)
        {
            // SourceDisksNames 1 = Setup Directory (e.g. \I386)
            SetSourceDisksFileDriverEntry(1, architectureIdentifier, fileName, fileCopyDisposition, destinationFileName);
        }

        public void SetSourceDisksFileDriverEntry(int sourceDiskID, string architectureIdentifier, string fileName, FileCopyDisposition fileCopyDisposition, string destinationFileName)
        {
            string sectionName = String.Format("SourceDisksFiles.{0}", architectureIdentifier);
            // first value is the sourceDiskID
            int lineIndex = GetLineIndexByFileNameAndWinnntDirectoryID(sectionName, fileName, (int)WinntDirectoryName.System32_Drivers);
            string newLine = GetSourceDisksFileDriverEntry(sourceDiskID, fileName, fileCopyDisposition, destinationFileName, this.MinorOSVersion);
            if (lineIndex == -1)
            {
                AppendLineToSection(sectionName, newLine);
            }
            else
            {
                UpdateLine(lineIndex, newLine);
            }
        }

        public void SetSourceDisksFileDllEntry(string architectureIdentifier, string fileName)
        {
            string sectionName = String.Format("SourceDisksFiles.{0}", architectureIdentifier);
            // first value is the sourceDiskID
            int lineIndex = GetLineIndexByFileNameAndWinnntDirectoryID(sectionName, fileName, (int)WinntDirectoryName.System32);
            string newLine = GetSourceDisksFileDllEntry(fileName, this.MinorOSVersion);
            if (lineIndex == -1)
            {
                AppendLineToSection(sectionName, newLine);
            }
            else
            {
                UpdateLine(lineIndex, newLine);
            }
        }

        public void SetSourceDisksFileEntry(string architectureIdentifier, int sourceDiskID, int destinationWinntDirectoryID, string fileName, FileCopyDisposition fileCopyDisposition)
        {
            string sectionName = String.Format("SourceDisksFiles.{0}", architectureIdentifier);
            // first value is the sourceDiskID
            int lineIndex = GetLineIndexByFileNameAndWinnntDirectoryID(sectionName, fileName, destinationWinntDirectoryID);
            string line = GetSourceDisksFileEntry(sourceDiskID, destinationWinntDirectoryID, fileName, false, fileCopyDisposition, fileCopyDisposition, String.Empty, this.MinorOSVersion);
            
            if (lineIndex == -1)
            {
                AppendLineToSection(sectionName, line);
            }
            else
            {
                UpdateLine(lineIndex, line);
            }
        }

        protected int GetLineIndexByFileNameAndWinnntDirectoryID(string sourceDiskFilesSectionName, string fileName, int winntDirectoryID)
        {
            Predicate<string> lineMatch = delegate(string line) { return GetKey(line).Equals(fileName, StringComparison.InvariantCultureIgnoreCase) && GetKeyAndValues(line).Value[7].Equals(winntDirectoryID.ToString(), StringComparison.InvariantCultureIgnoreCase); };
            string lineFound;
            return GetLineIndex(sourceDiskFilesSectionName, lineMatch, out lineFound);
        }

        public void AddDeviceToCriticalDeviceDatabase(string hardwareID, string serviceName)
        {
            AddDeviceToCriticalDeviceDatabase(hardwareID, serviceName, String.Empty);
        }

        public void AddDeviceToCriticalDeviceDatabase(string hardwareID, string serviceName, string classGUID)
        {
            string line = String.Format("{0} = {1}", hardwareID, Quote(serviceName));
            if (classGUID != String.Empty)
            {
                line += "," + classGUID;
            }

            int lineIndex = GetLineIndexByKey("HardwareIdsDatabase", hardwareID);
            if (lineIndex == -1)
            {
                AppendLineToSection("HardwareIdsDatabase", line);
            }
            else
            {
                UpdateLine(lineIndex, line);
            }
        }

        public void RemoveDeviceFromCriticalDeviceDatabase(string hardwareID)
        {
            string lineFound;
            int lineIndex = GetLineIndexByKey("HardwareIdsDatabase", hardwareID, out lineFound);
            if (lineIndex >= 0)
            {
                // Comment out this CDDB entry
                UpdateLine(lineIndex, ";" + lineFound);
            }
        }

        /// <summary>
        /// The section will be created if it does not exist
        /// </summary>
        public void AddFileToFilesSection(string sectionName, string fileName, int winntDirectoryID)
        {
            if (!SectionNames.Contains(sectionName))
            {
                AddSection(sectionName);
            }

            // Note: the key here is a combination of filename and directory,
            // so the same file could be copied to two directories.
            string entry = String.Format("{0},{1}", fileName, winntDirectoryID);
            int lineIndex = GetLineIndexByKey(sectionName, entry);
            if (lineIndex == -1)
            {
                AppendLineToSection(sectionName, entry);
            }
        }

        // Apparently this tells setup not to try to copy during GUI phase files that were already
        // copied and deleted from source during text mode, and will prevent copy error in some cases:
        // http://www.msfn.org/board/topic/94894-fileflags-section-of-txtsetupsif/
        // http://www.ryanvm.net/forum/viewtopic.php?t=1653
        // http://www.wincert.net/forum/index.php?/topic/1933-addon-genuine-advantage/
        public void SetFileFlagsEntryForDriver(string filename)
        {
            string line = String.Format("{0} = 16", filename);
            int lineIndex = GetLineIndex("FileFlags", line);
            if (lineIndex == -1)
            {
                AppendLineToSection("FileFlags", line);
            }
        }

        [Obsolete]
        public void UseMultiprocessorHal()
        {
            UseMultiprocessorHalForUniprocessorPC();
            EnableMultiprocessorHal();
        }

        [Obsolete]
        private void UseMultiprocessorHalForUniprocessorPC()
        {
            int lineIndex = GetLineIndexByKey("Hal.Load", "acpiapic_up");
            string updatedLine = "acpiapic_up = halmacpi.dll";
            UpdateLine(lineIndex, updatedLine);
        }

        [Obsolete]
        private void EnableMultiprocessorHal()
        {
            int lineIndex = GetLineIndexByKey("Hal.Load", "acpiapic_mp");
            string updatedLine = "acpiapic_mp = halmacpi.dll";
            UpdateLine(lineIndex, updatedLine);
        }

        /// <summary>
        /// This method will remove the /noguiboot switch and will enable the Windows logo to be displayed during text-mode setup,
        /// This is useful because some programs (namely sanbootconf) will print valuable debug information on top of the Windows logo.
        /// Historical note: /noguiboot is required when using monitors that do not support VGA resolution. (text-mode setup uses EGA resolution)
        /// </summary>
        public void EnableGUIBoot()
        {
            string line;
            int lineIndex = GetLineIndexByKey("SetupData", "OsLoadOptions", out line);
            KeyValuePair<string, List<string>> keyAndValues = GetKeyAndValues(line);
            string options = keyAndValues.Value[0];
            options = Unquote(options);
            List<string> optionList = StringUtils.Split(options, ' ');
            optionList.Remove("/noguiboot");
            options = StringUtils.Join(optionList, " ");
            options = Quote(options);
            string updatedLine = "OsLoadOptions = " + options;
            UpdateLine(lineIndex, updatedLine);
        }

        public int MinorOSVersion
        {
            get
            {
                List<string> values = GetValuesOfKeyInSection("SetupData", "MinorVersion");
                string value = TryGetValue(values, 0);
                int minorOSVersion = Conversion.ToInt32(value, -1);
                if (minorOSVersion == -1)
                {
                    Console.WriteLine("Error: '{0}' is corrupted.", this.FileName);
                    Program.Exit();
                }
                return minorOSVersion;
            }
        }

        private static string GetSourceDisksFileDllEntry(string fileName, int minorOSVersion)
        {
            return GetSourceDisksFileEntry(1, (int)WinntDirectoryName.System32, fileName, false, FileCopyDisposition.AlwaysCopy, FileCopyDisposition.AlwaysCopy, String.Empty, minorOSVersion);
        }

        private static string GetSourceDisksFileDriverEntry(int sourceDiskID, string fileName, FileCopyDisposition fileCopyDisposition, string destinationFileName, int minorOSVersion)
        {
            return GetSourceDisksFileEntry(sourceDiskID, (int)WinntDirectoryName.System32_Drivers, fileName, true, fileCopyDisposition, fileCopyDisposition, destinationFileName, minorOSVersion);
        }

        // not sure about most of the values here
        // http://www.msfn.org/board/topic/26742-nlite-not-processing-layoutinf/page__st__13
        // http://www.msfn.org/board/topic/125480-txtsetupsif-syntax/
        /// <param name="destinationFileName">leave Empty to keep the original name</param>
        private static string GetSourceDisksFileEntry(int sourceDiskID, int destinationWinntDirectoryID, string fileName, bool isDriver, FileCopyDisposition upgradeDisposition, FileCopyDisposition textModeDisposition, string destinationFileName, int minorOSVersion)
        {
            // here is sourceDiskID - 1st value
            string subdir = String.Empty; // values encountered: String.Empty
            
            string size = String.Empty; // values encountered: String.Empty
            string checksum = String.Empty; // values encountered: String.Empty, v
            string unused1 = String.Empty; // values encountered: String.Empty
            string unused2 = String.Empty; // values encountered: String.Empty

            // I believe trailing underscore means compressed (because compressed files have trailing underscore in their extension)
            // leading underscore apparently means the file is subject to a file-size check when copied - http://www.msfn.org/board/topic/127677-txtsetupsif-layoutinf-reference/
            // values encountered: String.Empty, _1, _3, _5, _6, _7, _x, 2_, 3_, 4_, 5_, 6_
            // after looking at [SourceDisksNames] in txtsetup.sif it seems that bootMediaOrder is referring to the floppy disk number from which to copy the file
            string bootMediaOrder; // 7th value
            if (isDriver)
            {
                bootMediaOrder = "4_"; //seems as good number as any, I believe that when installing from a CD, each of them refers to the $WINNT$.~BT folder
            }
            else
            {
                bootMediaOrder = String.Empty;
            }

            // here is winntDirectoryID - 8th value

            // here is upgradeDisposition - 9th value, values encountered: 0,1,2,3 
            
            // here is textModeDisposition - 10th value, values encountered: String.Empty,0,1,2,3

            string line = String.Format("{0} = {1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", fileName, sourceDiskID, subdir, size, checksum,
                                        unused1, unused2, bootMediaOrder, destinationWinntDirectoryID, (int)upgradeDisposition, (int)textModeDisposition);
            
            // here is destinationFileName - 11th value, actual filenames appear here (including long filenames)

            bool appendXP2003DriverEntries = isDriver && (minorOSVersion != 0);

            if (destinationFileName != String.Empty || appendXP2003DriverEntries)
            {
                line += "," + destinationFileName;
            }

            // the next 2 entries are only present in Windows XP / 2003, and usually used with .sys / .dll
            // I could not figure out what they do, the presence / omittance of these entries do not seem to have any effect during text-mode phase
            // note that GUI mode may use txtsetup.sif as well (copied to %windir%\$winnt$.sif if installing from /makelocalsource)
            if (appendXP2003DriverEntries) 
            {
                int unknownFlag = 1; // 12th value, values encountered: String.Empty,0,1
                int destinationDirectoryID = destinationWinntDirectoryID;
                line += String.Format(",{0},{1}", unknownFlag, destinationDirectoryID);
            }
            return line;
        }


        // Text-mode setup will always initialize services using the values stored under Services\serviceName
        // where serviceName is the service file name without the .sys extension
        public static string GetServiceName(string fileName)
        { 
            string serviceName = fileName.Substring(0, fileName.Length - 4);
            return serviceName;
        }
    }

    // Windows 2000 upgradecode && newinstallcode:
    // 0 - Always copies the file
    // 1 - Copies the file only if it exists in the installation directory
    // 2 - Does not copy the file if it exists in the installation directory
    // 3 - Does not copy the file (DEFAULT)

    /// <summary>
    /// Note that TXTSETUP.INF has its own copy directives in the [SCSI.Load], [BootBusExtenders], [BusExtenders], [InputDevicesSupport] and [Keyboard] sections,
    /// which are activated only if the hardware is present.
    /// This means that a file may be copied even if its FileCopyDisposition is set to DoNotCopy.
    /// </summary>
    public enum FileCopyDisposition
    {
        AlwaysCopy,
        CopyOnlyIfAlreadyExists,
        DoNotCopyIfAlreadyExists, // [SourceDisksFiles] entry will be used by [SCSI.Load] to copy the file if the hardware is present
        DoNotCopy,                // [SourceDisksFiles] entry will be used by [SCSI.Load] to copy the file if the hardware is present
    }

    public enum WinntDirectoryName
    {
        //Root = 1, // %windir%
        System32 = 2,
        System32_Drivers = 4,
        Temp = 45,
    }
}
