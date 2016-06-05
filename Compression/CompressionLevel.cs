//---------------------------------------------------------------------
// <copyright file="CompressionLevel.cs" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
//    
//    The use and distribution terms for this software are covered by the
//    Common Public License 1.0 (http://opensource.org/licenses/cpl.php)
//    which can be found in the file CPL.TXT at the root of this distribution.
//    By using this software in any fashion, you are agreeing to be bound by
//    the terms of this license.
//    
//    You must not remove this notice, or any other, from this software.
// </copyright>
// <summary>
// Part of the Deployment Tools Foundation project.
// </summary>
//---------------------------------------------------------------------

namespace Microsoft.Deployment.Compression
{
using System;
using System.Collections.Generic;
using System.Text;

    /// <summary>
    /// Specifies the compression level ranging from minimum compresion to
    /// maximum compression, or no compression at all.
    /// </summary>
    /// <remarks>
    /// Although only four values are enumerated, any integral value between
    /// <see cref="CompressionLevel.Min"/> and <see cref="CompressionLevel.Max"/> can also be used.
    /// </remarks>
    public enum CompressionLevel
    {
        /// <summary>Do not compress files, only store.</summary>
        None = 0,

        /// <summary>Minimum compression; fastest.</summary>
        Min = 1,

        /// <summary>A compromize between speed and compression efficiency.</summary>
        Normal = 6,

        /// <summary>Maximum compression; slowest.</summary>
        Max = 10
    }
}
