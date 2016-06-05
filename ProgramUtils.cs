using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace IntegrateDrv
{
    public class ProgramUtils
    {
        public static void CopyCriticalFile(string source, string destination)
        {
            CopyCriticalFile(source, destination, false);
        }

        public static void CopyCriticalFile(string source, string destination, bool promptFileCopied)
        {
            string fileName = FileSystemUtils.GetNameFromPath(source);
            FileSystemUtils.ClearReadOnlyAttribute(destination);
            try
            {
                // there will be a duplicity for NICs as GUIModeIntegrator takes care of them too
                File.Copy(source, destination, true);
                if (promptFileCopied)
                {
                    Console.WriteLine("File copied: " + fileName);
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Error: Missing file: " + source);
                Program.Exit();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Error: Access denied: Cannot copy: " + source);
                Program.Exit();
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("Error: Directory not found: Cannot copy: " + source);
                Program.Exit();
            }
            catch
            {
                Console.WriteLine("Error: Cannot copy: " + source);
                Program.Exit();
            }
        }
    }
}
