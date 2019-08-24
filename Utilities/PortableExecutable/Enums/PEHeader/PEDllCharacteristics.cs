
namespace Utilities
{
    public enum PEDllCharacteristics : ushort
    {
        DynamicBase = 0x0040,
        ForceIntegrity = 0x0080,
        NXCompatible = 0x0100,
        NoIsolation = 0x0200,
        NoSeh = 0x0400,
        NoBind = 0x0800,
        WdmDriver = 0x2000,
        TerminalServicesAware = 0x8000,
    }
}
