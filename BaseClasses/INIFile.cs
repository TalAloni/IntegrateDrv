using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    // Note: both .sif files and .inf files conform to .ini file specifications
    public partial class INIFile
    {
        private string m_fileName = String.Empty;
        private string m_text = String.Empty;
        private bool m_isModified = false;
        private List<string> m_sectionNamesCache;
        private Dictionary<string, List<string>> m_sectionCache = new Dictionary<string, List<string>>();
        private Encoding m_encoding = Encoding.ASCII; // we have to remeber the original encoding of the file, we use ASCII for new files

        public INIFile()
        {
        }

        /// <summary>
        /// Note regarding packed files: this method should be supplied with the unpacked file name!
        /// If the file is called nettcpip.in_, fileName should be nettcpip.inf!
        /// (this is the name in the packed container that we are looking for)
        /// </summary>
        public INIFile(string fileName)
        {
            m_fileName = fileName;
        }

        // Localized Windows editions and some drivers use Unicode encoding.
        // The supported formats are UTF-16 little endian and UTF-16 Big endian, and possibly UTF-8.
        private Encoding GetEncoding(ref byte[] fileBytes)
        { 
            if (fileBytes.Length >= 3)
            {
                if (fileBytes[0] == 0xEF &&
                    fileBytes[1] == 0xBB &&
                    fileBytes[2] == 0xBF)
                {
                    return Encoding.UTF8;
                }
            }

            if (fileBytes.Length >= 2)
            {
                if (fileBytes[0] == 0xFF &&
                    fileBytes[1] == 0xFE)
                {
                    return Encoding.Unicode;
                }

                if (fileBytes[0] == 0xFE &&
                    fileBytes[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode;
                }
            }
            // Note: Some localized versions of Windows use latin characters.
            //
            // During initial setup, the OEM code page specified under txtsetup.sif [nls] section will be used,
            // later, the ANSI codepage specified in that section will be used.
            //
            // It doesn't really matter which code page we use here as long as it preserves the non-ASCII characters
            // (both 437, 850, 1252 and 28591 works fine).
            //
            // Note: The only one that Mono supports is 28591.
            return Encoding.GetEncoding(28591);
        }

        /// <summary>
        /// File encoding is Ascii
        /// </summary>
        public void Read(string filePath)
        {
            byte[] bytes = new byte[0];
            try
            {
                bytes = FileSystemUtils.ReadFile(filePath);
            }
            catch (IOException)
            {
                // usually it means the device is not ready (disconnected network drive / CD-ROM)
                Console.WriteLine("Error: IOException, Could not read file: " + filePath);
                Program.Exit();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Error: Access Denied, Could not read file: " + filePath);
                Program.Exit();
            }
            catch
            {
                Console.WriteLine("Error: Could not read file: " + filePath);
                Program.Exit();
            }
            m_encoding = GetEncoding(ref bytes);
            string text = m_encoding.GetString(bytes);
            text = text.Replace("\r\r", "\r"); // fixes an issue with Windows 2000's hivesys.inf, String-reader will read \r\r as two lines, and this will screw-up broken lines
            m_text = text;
            
            ClearCache();
        }

        /// <summary>
        /// File encoding is Ascii
        /// </summary>
        public void ReadFromDirectory(string directoryPath)
        {
            if (m_fileName == String.Empty)
            {
                throw new Exception("ReadFileFromDirectory - class has not been initizalized with a file name");
            }
            Read(directoryPath + m_fileName);
        }

        private void ClearCache()
        {
            m_sectionNamesCache = null;
            m_sectionCache = new Dictionary<string, List<string>>();
        }

        private void ClearCachedSection(string sectionName)
        {
            // m_sectionCache stores sectionName in lowercase
            m_sectionCache.Remove(sectionName.ToLower());
        }

        public List<string> GetSection(string sectionName)
        {
            // m_sectionCache stores sectionName in lowercase
            string sectionCacheKey = sectionName.ToLower();
            if (m_sectionCache.ContainsKey(sectionCacheKey))
            {
                return m_sectionCache[sectionCacheKey];
            }
            else
            {
                List<string> section = GetSectionInText(sectionName, m_text);
                m_sectionCache.Add(sectionCacheKey, section);
                return section;
            }
        }

        public string GetFirstValueOfKeyInSection(string sectionName, string key)
        {
            List<string> values = GetValuesOfKeyInSection(sectionName, key);
            if (values.Count > 0)
            {
                return values[0];
            }
            else
            {
                return String.Empty;
            }
        }

        public List<string> GetValuesOfKeyInSection(string sectionName, string key)
        {
            List<string> section = GetSection(sectionName);
            foreach (string line in section)
            {
                KeyValuePair<string, List<string>> keyAndValues = GetKeyAndValues(line);
                if (keyAndValues.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                {
                    return keyAndValues.Value;
                }
            }
            return new List<string>();
        }

        public List<string> SectionNames
        {
            get
            {
                if (m_sectionNamesCache == null)
                {
                    m_sectionNamesCache = ListSections(m_text);
                }
                return m_sectionNamesCache;
            }
        }

        // Note: it is valid to have an empty section (e.g. [files.none])
        public void AddSection(string sectionName)
        {
            // Add an empty line before section header
            AppendLine(String.Empty);
            AppendLine("[" + sectionName + "]");
            if (m_sectionNamesCache != null)
            {
                m_sectionNamesCache.Add(sectionName);
            }
        }

        protected void AppendLineToSection(string sectionName, string lineToAppend)
        {
            StringWriter writer = new StringWriter();
            StringReader reader = new StringReader(m_text);
            int index = 0;

            int sectionHeaderLastIndex = GetLastIndexOfSectionHeader(sectionName);
            // we insert one line after the last non-empty line, search start from one line after the section header
            int insertIndex = GetLastIndexOfNonEmptyLineInSection(sectionHeaderLastIndex + 1) + 1;
            bool done = false;
            string line = reader.ReadLine();
            while (line != null)
            {
                if (index == insertIndex)
                {
                    writer.WriteLine(lineToAppend);
                    done = true;
                }
                writer.WriteLine(line);

                line = reader.ReadLine();
                index++;
            }
            if (done == false)
            {
                writer.WriteLine(lineToAppend);
            }
            m_text = writer.ToString();
            
            m_isModified = true;
            ClearCachedSection(sectionName);
        }

        protected void InsertLine(int lineIndex, string lineToInsert)
        {
            StringWriter writer = new StringWriter();
            StringReader reader = new StringReader(m_text);
            int index = 0;

            string line = reader.ReadLine();
            while (line != null)
            {
                if (index == lineIndex)
                {
                    writer.WriteLine(lineToInsert);
                }
                writer.WriteLine(line);

                line = reader.ReadLine();
                index++;
            }
            m_text = writer.ToString();
            
            m_isModified = true;
            ClearCache();
        }

        protected void AppendLine(string lineToAppend)
        {
            // Windows 2000: txtsetup.sif usually ends with an EOF marker followed by "\r\n".
            // Windows XP x86: txtsetup.sif usually ends with an EOF marker.
            // Note: In both cases, the EOF marker is not required.
            // Note: If an EOF is present, lines following it will be ignored.
            char eof = (char)0x1A;
            if (m_text.EndsWith(eof.ToString()))
            {
                m_text = m_text.Remove(m_text.Length - 1);
            }
            else if (m_text.EndsWith(eof + "\r\n"))
            {
                m_text = m_text.Remove(m_text.Length - 3);
            }

            if (!m_text.EndsWith("\r\n"))
            {
                m_text += "\r\n";
            }
            m_text += lineToAppend + "\r\n";

            m_isModified = true;
            ClearCache();
        }

        protected void DeleteLine(int lineIndex)
        {
            DeleteLine(lineIndex, true);
        }

        protected void DeleteLine(int lineIndex, bool removeTrailingBrokenLines)
        {
            UpdateLine(lineIndex, null, removeTrailingBrokenLines);
        }

        protected void UpdateLine(int lineIndex, string updatedLine)
        {
            UpdateLine(lineIndex, updatedLine, false);
        }

        protected void UpdateLine(int lineIndex, string updatedLine, bool removeTrailingBrokenLines)
        {
            StringWriter writer = new StringWriter();
            StringReader reader = new StringReader(m_text);
            int index = 0;

            string line = reader.ReadLine();
            while (line != null)
            {
                if (index == lineIndex)
                {
                    if (updatedLine != null)
                    {
                        writer.WriteLine(updatedLine);
                    }
                    if (removeTrailingBrokenLines)
                    {
                        while (line.EndsWith(@"\"))
                        {
                            line = reader.ReadLine();
                        }
                    }
                }
                else
                {
                    writer.WriteLine(line);
                }

                line = reader.ReadLine();
                index++;
            }
            m_text = writer.ToString();

            m_isModified = true;
            ClearCache();
        }

        /// <summary>
        /// The same section can appear multiple times in a single file
        /// </summary>
        private int GetLastIndexOfSectionHeader(string sectionName)
        {
            int index = 0;
            int lastIndex = -1;
            StringReader reader = new StringReader(m_text);
            string sectionHeader = String.Format("[{0}]", sectionName);
            
            string line = reader.ReadLine();
            while (line != null)
            {
                if (line.TrimStart(' ').StartsWith(sectionHeader, StringComparison.InvariantCultureIgnoreCase))
                {
                    lastIndex = index;
                }

                line = reader.ReadLine();
                index++;
            }

            return lastIndex;
        }

        private int GetLastIndexOfNonEmptyLineInSection(int startIndex)
        {
            int index = 0;
            int lastIndex = startIndex -  1;
            StringReader reader = new StringReader(m_text);
            string line = reader.ReadLine();
            while (line != null)
            {
                if (index >= startIndex)
                {
                    if (IsSectionHeader(line))
                    {
                        return lastIndex;
                    }

                    if (line.Trim() != String.Empty)
                    {
                        lastIndex = index;
                    }
                }
                line = reader.ReadLine();
                index++;
            }

            if (lastIndex == -1)
            {
                lastIndex = index;
            }
            return lastIndex;
        }

        virtual public void SaveToDirectory(string directory)
        {
            Save(directory + m_fileName);
        }

        virtual public void Save(string path)
        {
            // if an existing file was read, m_text will contain the BOM character, otherwise we write ASCII and there is no need for BOM.
            byte[] bytes = m_encoding.GetBytes(m_text);
            FileSystemUtils.ClearReadOnlyAttribute(path);
            FileSystemUtils.WriteFile(path, bytes);
            m_isModified = false;
        }

        protected static List<string> GetSectionInText(string sectionName, string text)
        {
            List<string> result = new List<string>();
            StringReader reader = new StringReader(text);
            string sectionHeader = String.Format("[{0}]", sectionName);
            bool outsideSection = true;
            string line = reader.ReadLine();
            while (line != null)
            {
                if (outsideSection)
                {
                    if (line.TrimStart(' ').StartsWith(sectionHeader, StringComparison.InvariantCultureIgnoreCase))
                    {
                        outsideSection = false;
                    }
                }
                else
                {
                    if (IsSectionHeader(line))
                    {
                        // section ended, but the same section can appear multiple times inside a single file
                        outsideSection = true;
                        continue;
                    }

                    if (!IsComment(line) && line.Trim() != String.Empty)
                    {
                        result.Add(line);
                    }
                }

                line = reader.ReadLine();
            }
            return result;
        }

        protected static List<string> ListSections(string text)
        {
            List<string> result = new List<string>();
            StringReader reader = new StringReader(text);
            string line = reader.ReadLine();
            while (line != null)
            {
                if (IsSectionHeader(line))
                {
                    int sectionNameStart = line.IndexOf('[') + 1;
                    int sectionNameEnd = line.IndexOf(']', sectionNameStart + 1) - 1;
                    if (sectionNameStart >= 0 && sectionNameEnd > sectionNameStart)
                    {
                        string sectionName = line.Substring(sectionNameStart, sectionNameEnd - sectionNameStart + 1);
                        // the same section can appear multiple times inside a single file
                        if (!StringUtils.ContainsCaseInsensitive(result, sectionName))
                        {
                            result.Add(sectionName);
                        }
                    }
                }
                line = reader.ReadLine();

            }
            return result;
        }

        public static string GetKey(string line)
        {
            return GetKeyAndValues(line).Key;
        }

        public static KeyValuePair<string, List<string>> GetKeyAndValues(string line)
        {
            int index = line.IndexOf("=");
            if (index >= 0)
            {
                string key = line.Substring(0, index).Trim();
                string value = line.Substring(index + 1);
                List<string> values = GetCommaSeparatedValues(value);

                return new KeyValuePair<string, List<string>>(key, values);
            }
            else
            {
                return new KeyValuePair<string, List<string>>(line, new List<string>());
            }
        }

        public static List<string> GetCommaSeparatedValues(string value)
        {
            int commentIndex = QuotedStringUtils.IndexOfUnquotedChar(value, ';');
            if (commentIndex >= 0)
            {
                value = value.Substring(0, commentIndex);
            }
            List<string> values = QuotedStringUtils.SplitIgnoreQuotedSeparators(value, ',');
            for (int index = 0; index < values.Count; index++)
            {
                values[index] = values[index].Trim();
            }
            return values;
        }

        protected int GetLineIndex(string sectionName, string lineToFind)
        {
            Predicate<string> lineEquals = delegate(string line) { return line.Equals(lineToFind, StringComparison.InvariantCultureIgnoreCase); };
            string lineFound;
            return GetLineIndex(sectionName, lineEquals, out lineFound);
        }

        protected int GetLineIndexByKey(string sectionName, string key)
        {
            string lineFound;
            return GetLineIndexByKey(sectionName, key, out lineFound);
        }

        protected int GetLineIndexByKey(string sectionName, string key, out string lineFound)
        {
            Predicate<string> lineKeyMatch = delegate(string line) { return GetKey(line).Equals(key, StringComparison.InvariantCultureIgnoreCase); };
            return GetLineIndex(sectionName, lineKeyMatch, out lineFound);
        }

        protected int GetLineIndex(string sectionName, Predicate<string> lineFilter, out string lineFound)
        {
            return GetLineIndex(sectionName, lineFilter, out lineFound, false);
        }
        /// <summary>
        /// Will return the index of the first line (lineIndex, line) in the section for which the predicate will return true
        /// </summary>
        protected int GetLineIndex(string sectionName, Predicate<string> lineFilter, out string lineFound, bool appendBrokenLines)
        {
            StringReader reader = new StringReader(this.Text);
            string sectionHeader = String.Format("[{0}]", sectionName);
            bool outsideSection = true;
            string line = reader.ReadLine();
            int index = 0;
            while (line != null)
            {
                if (outsideSection)
                {
                    if (line.TrimStart(' ').StartsWith(sectionHeader, StringComparison.InvariantCultureIgnoreCase)) // section header could have a comment following
                    {
                        outsideSection = false;
                    }
                }
                else
                {
                    if (IsSectionHeader(line))
                    {
                        // section ended, but the same section can appear multiple times inside a single file
                        outsideSection = true;
                        continue;
                    }

                    if (lineFilter(line))
                    {
                        lineFound = line;
                        if (appendBrokenLines)
                        {
                            while (line.EndsWith(@"\")) // value data will continuer in next line 
                            {
                                lineFound = lineFound.Substring(0, lineFound.Length - 1); // remove trailing slash
                                line = reader.ReadLine();
                                lineFound = lineFound + line.TrimStart(' ');
                            }
                        }
                        return index;
                    }
                }

                line = reader.ReadLine();
                index++;
            }
            lineFound = String.Empty;
            return -1;
        }

        protected static bool IsSectionHeader(string line)
        {
            return line.TrimStart(' ').StartsWith("[");
        }

        protected static bool IsComment(string line)
        {
            return (line.TrimStart(' ').StartsWith(";") || line.TrimStart(' ').StartsWith("#"));
        }

        public static string Quote(string str)
        {
            return QuotedStringUtils.Quote(str);
        }

        public static string Unquote(string str)
        {
            return QuotedStringUtils.Unquote(str);
        }

        public string FileName
        {
            get
            {
                return m_fileName;
            }
        }

        protected string Text
        {
            get
            {
                return m_text;
            }
            set
            {
                m_text = value;
            }
        }

        public bool IsModified
        {
            get
            {
                return m_isModified;
            }
            protected set
            {
                m_isModified = value;
            }
        }

        public static string TryGetValue(List<string> values, int valueIndex)
        {
            string result = String.Empty;
            if (values.Count > valueIndex)
            {
                result = values[valueIndex];
            }
            return result;
        }
    }
}
