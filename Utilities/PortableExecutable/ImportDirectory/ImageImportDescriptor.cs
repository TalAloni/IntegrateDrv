/* Copyright (C) 2011 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * Based on work by jachymko, Dec 24, 2006.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;

namespace Utilities
{
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
