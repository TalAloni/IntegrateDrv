using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utilities
{
	public partial class StringUtils
	{
        public static List<string> Split(string text, char separator)
        {
            List<string> result = new List<string>(text.Split(separator));
            return result;
        }
         
        public static string Join(List<string> parts)
        {
            return Join(parts, String.Empty);
        }

        public static string Join(List<string> parts, string separator)
        {
            StringBuilder sBuilder = new StringBuilder();
            for (int index = 0; index < parts.Count; index++)
            {
                if (index != 0)
                {
                    sBuilder.Append(separator);
                }
                sBuilder.Append(parts[index]);
            }
            return sBuilder.ToString();
        }

        public static bool ContainsCaseInsensitive(List<string> list, string value)
        {
            return (IndexOfCaseInsensitive(list, value) != -1);
        }

        public static int IndexOfCaseInsensitive(List<string> list, string value)
        {
            for(int index = 0; index < list.Count; index++)
            {
                if (list[index].Equals(value, StringComparison.InvariantCultureIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }
	}
}
