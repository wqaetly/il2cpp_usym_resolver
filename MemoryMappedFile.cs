using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Il2CppSymbolReader;

/// <summary>
/// 简化的内存映射文件实现
/// </summary>
public class MemoryMappedFile : IDisposable
{
    private readonly SafeFileHandle _fileHandle;
    private readonly SafeMemoryMappedFileHandle _mappingHandle;
    private readonly long _capacity;
    
    private MemoryMappedFile(SafeFileHandle fileHandle, SafeMemoryMappedFileHandle mappingHandle, long capacity)
    {
        _fileHandle = fileHandle;
        _mappingHandle = mappingHandle;
        _capacity = capacity;
    }
    
    /// <summary>
    /// 从文件创建内存映射文件
    /// </summary>
    public static MemoryMappedFile CreateFromFile(string path, FileMode mode, string mapName, long capacity, MemoryMappedFileAccess access)
    {
        var fileAccess = access switch
        {
            MemoryMappedFileAccess.Read => FileAccess.Read,
            MemoryMappedFileAccess.Write => FileAccess.Write,
            MemoryMappedFileAccess.ReadWrite => FileAccess.ReadWrite,
            _ => FileAccess.Read
        };
        
        var fileHandle = File.OpenHandle(path, mode, fileAccess, FileShare.Read);
        var fileLength = RandomAccess.GetLength(fileHandle);
        
        if (capacity == 0)
            capacity = fileLength;
            
        var protection = access switch
        {
            MemoryMappedFileAccess.Read => 0x02, // PAGE_READONLY
            MemoryMappedFileAccess.Write => 0x04, // PAGE_READWRITE
            MemoryMappedFileAccess.ReadWrite => 0x04, // PAGE_READWRITE
            _ => 0x02
        };
        
        var mappingHandle = CreateFileMapping(fileHandle, IntPtr.Zero, (uint)protection, 0, 0, mapName);
        if (mappingHandle.IsInvalid)
        {
            fileHandle.Dispose();
            throw new InvalidOperationException("Failed to create file mapping");
        }
        
        return new MemoryMappedFile(fileHandle, mappingHandle, capacity);
    }
    
    /// <summary>
    /// 创建视图访问器
    /// </summary>
    public MemoryMappedViewAccessor CreateViewAccessor(long offset, long size, MemoryMappedFileAccess access)
    {
        if (size == 0)
            size = _capacity - offset;
            
        var desiredAccess = access switch
        {
            MemoryMappedFileAccess.Read => 0x0004, // FILE_MAP_READ
            MemoryMappedFileAccess.Write => 0x0002, // FILE_MAP_WRITE
            MemoryMappedFileAccess.ReadWrite => 0x0006, // FILE_MAP_READ | FILE_MAP_WRITE
            _ => 0x0004
        };
        
        var ptr = MapViewOfFile(_mappingHandle, (uint)desiredAccess, (uint)(offset >> 32), (uint)offset, (UIntPtr)size);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to map view of file");
        }
        
        return new MemoryMappedViewAccessor(ptr, size);
    }
    
    public void Dispose()
    {
        _mappingHandle?.Dispose();
        _fileHandle?.Dispose();
    }
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeMemoryMappedFileHandle CreateFileMapping(
        SafeFileHandle hFile,
        IntPtr lpFileMappingAttributes,
        uint flProtect,
        uint dwMaximumSizeHigh,
        uint dwMaximumSizeLow,
        string? lpName);
        
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(
        SafeMemoryMappedFileHandle hFileMappingObject,
        uint dwDesiredAccess,
        uint dwFileOffsetHigh,
        uint dwFileOffsetLow,
        UIntPtr dwNumberOfBytesToMap);
        
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
}

/// <summary>
/// 安全的内存映射文件句柄
/// </summary>
public class SafeMemoryMappedFileHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeMemoryMappedFileHandle() : base(true) { }
    
    protected override bool ReleaseHandle()
    {
        return CloseHandle(handle);
    }
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern SafeMemoryMappedFileHandle CreateFileMapping(
        SafeFileHandle hFile,
        IntPtr lpFileMappingAttributes,
        uint flProtect,
        uint dwMaximumSizeHigh,
        uint dwMaximumSizeLow,
        string? lpName);
}

/// <summary>
/// 内存映射文件访问模式
/// </summary>
public enum MemoryMappedFileAccess
{
    Read,
    Write,
    ReadWrite
}

/// <summary>
/// 内存映射视图访问器
/// </summary>
public class MemoryMappedViewAccessor : IDisposable
{
    private readonly IntPtr _baseAddress;
    private readonly long _capacity;
    private bool _disposed;
    
    internal MemoryMappedViewAccessor(IntPtr baseAddress, long capacity)
    {
        _baseAddress = baseAddress;
        _capacity = capacity;
    }
    
    public long Capacity => _capacity;
    
    public void ReadArray<T>(long position, T[] array, int offset, int count) where T : struct
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemoryMappedViewAccessor));
            
        var elementSize = Marshal.SizeOf<T>();
        var totalBytes = count * elementSize;
        
        if (position + totalBytes > _capacity)
            throw new ArgumentOutOfRangeException(nameof(position));
            
        unsafe
        {
            var src = (byte*)_baseAddress + position;
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            try
            {
                var dst = (byte*)handle.AddrOfPinnedObject() + (offset * elementSize);
                Buffer.MemoryCopy(src, dst, totalBytes, totalBytes);
            }
            finally
            {
                handle.Free();
            }
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            UnmapViewOfFile(_baseAddress);
            _disposed = true;
        }
    }
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
}