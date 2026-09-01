using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.DevHarness;

public unsafe class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BambooMintKey NativeAOT Dev Harness ===");

        // TODO: Load BambooMintKey.dll, call DllGetClassObject, create TIP instance and test interfaces per 002_05
        throw new NotImplementedException("Dev harness implementation pending per 002_05.");
    }
}
