/* Copyright (C) 2011 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * Based on work by jachymko, Dec 24, 2006.
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
    public class ImportDirectory
    {
        public List<ImageImportDescriptor> Descriptors = new List<ImageImportDescriptor>();

        public static ImportDirectory Parse(BinaryReader reader)
        {
            ImportDirectory importDir = new ImportDirectory();
            ImageImportDescriptor desc = ImageImportDescriptor.Parse(reader);
            while (desc.NameRVA != 0)
            {
                importDir.Descriptors.Add(desc);
                desc = ImageImportDescriptor.Parse(reader);
            }
            
            return importDir;
        }

        public void Write(BinaryWriter writer)
        {
            foreach (ImageImportDescriptor descriptor in Descriptors)
            {
                descriptor.Write(writer);
            }
            new ImageImportDescriptor().Write(writer);
        }
    }
}
