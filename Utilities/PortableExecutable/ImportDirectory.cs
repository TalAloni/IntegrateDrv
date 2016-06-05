// Based on work by jachymko, Dec 24, 2006
// Adapted by Tal Aloni, 2011.09.09

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

    public class ImageImportDescriptor
    {
        public uint ImportLookupTableRVA;
        public uint TimeDateStamp;
        public uint ForwardChain;
        public uint NameRVA;
        public uint ImportAddressTableRVA;

        public static ImageImportDescriptor Parse(BinaryReader reader)
        {
            ImageImportDescriptor descriptor = new ImageImportDescriptor();
            descriptor.ImportLookupTableRVA = reader.ReadUInt32();
            descriptor.TimeDateStamp = reader.ReadUInt32();
            descriptor.ForwardChain = reader.ReadUInt32();
            descriptor.NameRVA = reader.ReadUInt32();
            descriptor.ImportAddressTableRVA = reader.ReadUInt32();
            return descriptor;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(ImportLookupTableRVA);
            writer.Write(TimeDateStamp);
            writer.Write(ForwardChain);
            writer.Write(NameRVA);
            writer.Write(ImportAddressTableRVA);
        }
    }
}
