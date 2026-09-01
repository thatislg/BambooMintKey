namespace BambooMintKey.NativeBridge.COM;

/// <summary>
/// Quản lý số lượng tham chiếu đang sống của COM server trong tiến trình.
/// Windows TSF sẽ gọi <see cref="BambooMintKey.NativeBridge.Exports.DllCanUnloadNow"/> để kiểm tra xem DLL có
/// thể bị gỡ bỏ khỏi bộ nhớ hay chưa. Lớp này đảm bảo DLL không bị gỡ
/// trong khi vẫn còn đối tượng hoặc khóa server đang hoạt động.
/// Theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// </summary>
public static class ComServerState
{
    // =========================================================================
    // Bộ đếm tham chiếu
    // =========================================================================

    /// <summary>
    /// Số lần COM client gọi IClassFactory::LockServer(TRUE) hoặc
    /// server được giữ lại để ngăn unload sớm.
    /// Chỉ khi về 0 mới được phép gỡ DLL.
    /// </summary>
    private static int _lockCount;

    /// <summary>
    /// Số đối tượng COM (ví dụ: BambooMintKeyTextService) đang tồn tại trong bộ nhớ.
    /// Mỗi lần tạo +1, mỗi lần release -1.
    /// </summary>
    private static int _objectCount;

    // =========================================================================
    // Quản lý khóa server (LockServer)
    // =========================================================================

    /// <summary>
    /// Tăng bộ đếm khóa server. Gọi khi COM client khóa server
    /// (IClassFactory::LockServer fLock = 1) hoặc khi cần giữ DLL sống.
    /// </summary>
    public static void Lock() => Interlocked.Increment(ref _lockCount);

    /// <summary>
    /// Giảm bộ đếm khóa server. Gọi khi COM client mở khóa
    /// (IClassFactory::LockServer fLock = 0).
    /// </summary>
    public static void Unlock() => Interlocked.Decrement(ref _lockCount);

    // =========================================================================
    // Quản lý đối tượng COM
    // =========================================================================

    /// <summary>
    /// Tăng bộ đếm đối tượng đang sống. Gọi trong CreateNativeInstance
    /// của BambooMintKeyTextService mỗi khi tạo một instance mới.
    /// </summary>
    public static void ObjectCreated() => Interlocked.Increment(ref _objectCount);

    /// <summary>
    /// Giảm bộ đếm đối tượng đang sống. Gọi trong Release khi đối tượng
    /// COM cuối cùng bị giải phóng (refCount == 0).
    /// </summary>
    public static void ObjectDestroyed() => Interlocked.Decrement(ref _objectCount);

    // =========================================================================
    // Kiểm tra điều kiện unload
    // =========================================================================

    /// <summary>
    /// Trả về true nếu DLL có thể an toàn bị gỡ bỏ khỏi bộ nhớ:
    /// - Không còn khóa server nào đang giữ.
    /// - Không còn đối tượng COM nào đang sống.
    /// Kết quả này được DllCanUnloadNow trả về cho Windows.
    /// </summary>
    public static bool CanUnload => Volatile.Read(ref _lockCount) == 0 && Volatile.Read(ref _objectCount) == 0;
}
