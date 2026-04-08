namespace Munt.NET;

public enum Mt32EmuReturnCode
{
    OK = 0,
    AddedControlRom = 1,
    AddedPcmRom = 2,
    AddedPartialControlRom = 3,
    AddedPartialPcmRom = 4,
    MissingRoms = -1,
    FileNotFound = -2,
    FileNotLoaded = -3,
    NotOpened = -4,
    QueueFull = -5,
}
