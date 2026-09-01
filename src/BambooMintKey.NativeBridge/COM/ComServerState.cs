namespace BambooMintKey.NativeBridge.COM;

public static class ComServerState
{
    private static int _lockCount;
    private static int _objectCount;

    public static void Lock() => Interlocked.Increment(ref _lockCount);
    public static void Unlock() => Interlocked.Decrement(ref _lockCount);
    public static void ObjectCreated() => Interlocked.Increment(ref _objectCount);
    public static void ObjectDestroyed() => Interlocked.Decrement(ref _objectCount);

    public static bool CanUnload => Volatile.Read(ref _lockCount) == 0 && Volatile.Read(ref _objectCount) == 0;
}
