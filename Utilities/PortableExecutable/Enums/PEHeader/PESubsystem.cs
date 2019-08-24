
namespace Utilities
{
    public enum PESubsystem : ushort
    {
        Unknown = 0,
        Native = 1,
        Windows = 2,
        WindowsConsole = 3,
        OS2 = 5,
        Posix = 7,
        WindowsCE = 9,
        EfiApplication = 10,
        EfiBootServiceDriver = 11,
        EfiRuntimeDriver = 12,
        EfiRomImage = 13,
        Xbox = 14,
        WindowsBootApplication = 16,
    }
}
