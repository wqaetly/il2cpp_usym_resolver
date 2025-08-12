using System.Runtime.InteropServices;

namespace Il2CppSymbolReader;

/// <summary>
/// usym文件头结构
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UsymHeader
{
    /// <summary>
    /// 魔术数字，应为0x2D6D7973 ("sym-")
    /// </summary>
    public uint Magic;
    
    /// <summary>
    /// 版本号
    /// </summary>
    public uint Version;
    
    /// <summary>
    /// 行数
    /// </summary>
    public uint LineCount;
    
    /// <summary>
    /// 可执行文件ID，字符串表中的偏移量
    /// </summary>
    public uint Id;
    
    /// <summary>
    /// 操作系统，字符串表中的偏移量
    /// </summary>
    public uint Os;
    
    /// <summary>
    /// 架构，字符串表中的偏移量
    /// </summary>
    public uint Arch;
}

/// <summary>
/// usym文件行结构
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UsymLine
{
    /// <summary>
    /// 内存地址
    /// </summary>
    public ulong Address;
    
    /// <summary>
    /// 方法索引
    /// </summary>
    public uint MethodIndex;
    
    /// <summary>
    /// 源文件名，字符串表中的偏移量
    /// </summary>
    public uint FileName;
    
    /// <summary>
    /// 源代码行号
    /// </summary>
    public uint Line;
    
    /// <summary>
    /// 父级索引
    /// </summary>
    public uint Parent;
}

/// <summary>
/// 符号信息结果
/// </summary>
public struct SymbolInfo
{
    /// <summary>
    /// 源文件路径
    /// </summary>
    public string? FileName { get; init; }
    
    /// <summary>
    /// 行号
    /// </summary>
    public uint LineNumber { get; init; }
    
    /// <summary>
    /// 方法索引
    /// </summary>
    public uint MethodIndex { get; init; }
    
    /// <summary>
    /// 父级信息
    /// </summary>
    public uint Parent { get; init; }
    
    /// <summary>
    /// 原始地址
    /// </summary>
    public ulong Address { get; init; }
}