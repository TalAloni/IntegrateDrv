/* Copyright (C) 2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace Utilities
{
    public class ResourceDirectoryTable
    {
        public ResourceDirectoryTableHeader Header;
        public List<ResourceDirectoryEntry> NameEntries = new List<ResourceDirectoryEntry>();
        public List<ResourceDirectoryEntry> IDEntries = new List<ResourceDirectoryEntry>();

        public static ResourceDirectoryTable Parse(BinaryReader reader)
        {
            ResourceDirectoryTable table = new ResourceDirectoryTable();
            table.Header = ResourceDirectoryTableHeader.Parse(reader);
            for (int index = 0; index < table.Header.NumberOfNameEntries; index++)
            {
                ResourceDirectoryEntry entry = ResourceDirectoryEntry.Parse(reader);
                table.NameEntries.Add(entry);
            }

            for (int index = 0; index < table.Header.NumberOfIDEntries; index++)
            {
                ResourceDirectoryEntry entry = ResourceDirectoryEntry.Parse(reader);
                table.IDEntries.Add(entry);
            }
            return table;
        }
    }
}
