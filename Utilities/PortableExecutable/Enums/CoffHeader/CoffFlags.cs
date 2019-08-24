using System;

namespace Utilities
{
    [Flags]
    public enum CoffFlags : ushort
    {
        RelocsStripped = 0x0001,
        ExecutableImage = 0x0002,
        LineNumsStripped = 0x0004,
        LocalSymsStripped = 0x0008,
        AggresiveWsTrim = 0x0010,
        LargeAddressAware = 0x0020,
        BytesReversedLow = 0x0080,
        Machine32Bit = 0x0100,
        DebugStripped = 0x0200,
        RemovableRunFromSwap = 0x0400,
        NetworkRunFromSwap = 0x0800,
        System = 0x1000,
        Dll = 0x2000,
        UniProcOnly = 0x4000,
        BytesReversedHi = 0x8000,
    }
}
