using System.Text.RegularExpressions;

namespace Il2CppSymbolReader;

/// <summary>
/// Unity IL2CPP 堆栈跟踪解析器
/// 解析类似以下格式的堆栈跟踪：
/// KF.ProtoBufRuntimeTypeModelLogicLayer.Init () (at 0x42299e6:0)
/// KF.BaseDataConfigManager.InitData () (at 0x3ede55a:0)
/// </summary>
public class UnityStackTraceParser
{
    private static readonly Regex StackTracePattern = new(
        @"^(?<className>[\w\.<>\+`]+)\.(?<methodName>[\w<>\+`]+)\s*\((?<parameters>.*?)\)\s*\(at\s+(?<address>0x[0-9a-fA-F]+):(?<offset>\d+)\)$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    
    /// <summary>
    /// 堆栈跟踪条目
    /// </summary>
    public struct StackTraceEntry
    {
        public string ClassName { get; init; }
        public string MethodName { get; init; }
        public string Parameters { get; init; }
        public string Address { get; init; }
        public int Offset { get; init; }
        public string FullMethodSignature => $"{ClassName}.{MethodName}({Parameters})";
        public string OriginalLine { get; init; }
    }
    
    /// <summary>
    /// 解析结果
    /// </summary>
    public struct ParseResult
    {
        public List<StackTraceEntry> Entries { get; init; }
        public List<string> Addresses { get; init; }
        public bool IsValid { get; init; }
    }
    
    /// <summary>
    /// 解析 Unity 堆栈跟踪文本
    /// </summary>
    /// <param name="stackTrace">堆栈跟踪文本</param>
    /// <returns>解析结果</returns>
    public static ParseResult Parse(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return new ParseResult { IsValid = false, Entries = new List<StackTraceEntry>(), Addresses = new List<string>() };
        }
        
        var matches = StackTracePattern.Matches(stackTrace);
        var entries = new List<StackTraceEntry>();
        var addresses = new List<string>();
        
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                var entry = new StackTraceEntry
                {
                    ClassName = match.Groups["className"].Value,
                    MethodName = match.Groups["methodName"].Value,
                    Parameters = match.Groups["parameters"].Value,
                    Address = match.Groups["address"].Value,
                    Offset = int.TryParse(match.Groups["offset"].Value, out int offset) ? offset : 0,
                    OriginalLine = match.Value.Trim()
                };
                
                entries.Add(entry);
                addresses.Add(entry.Address);
            }
        }
        
        return new ParseResult
        {
            Entries = entries,
            Addresses = addresses,
            IsValid = entries.Count > 0
        };
    }
    
    /// <summary>
    /// 从文本中提取所有地址
    /// </summary>
    /// <param name="text">包含地址的文本</param>
    /// <returns>地址列表</returns>
    public static List<string> ExtractAddresses(string text)
    {
        var result = Parse(text);
        return result.Addresses;
    }
    
    /// <summary>
    /// 检查文本是否包含 Unity 堆栈跟踪格式
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <returns>是否包含 Unity 堆栈跟踪</returns>
    public static bool ContainsUnityStackTrace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        return StackTracePattern.IsMatch(text);
    }
    
    /// <summary>
    /// 验证单行是否为有效的 Unity 堆栈跟踪格式
    /// </summary>
    /// <param name="line">单行文本</param>
    /// <returns>是否有效</returns>
    public static bool IsValidStackTraceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;
            
        return StackTracePattern.IsMatch(line.Trim());
    }
}

/// <summary>
/// Unity 堆栈跟踪解析和解析结果
/// </summary>
public class UnityStackTraceResolver : IDisposable
{
    private readonly Il2CppAddressResolver _addressResolver;
    
    public UnityStackTraceResolver(string symbolFilePath, ulong imageBase = 0)
    {
        _addressResolver = new Il2CppAddressResolver(symbolFilePath, imageBase);
    }
    
    /// <summary>
    /// 解析并解析 Unity 堆栈跟踪
    /// </summary>
    /// <param name="stackTraceText">堆栈跟踪文本</param>
    /// <returns>解析结果</returns>
    public UnityStackTraceResult? ResolveStackTrace(string stackTraceText)
    {
        var parseResult = UnityStackTraceParser.Parse(stackTraceText);
        if (!parseResult.IsValid)
            return null;
        
        var resolvedFrames = new List<ResolvedStackFrame>();
        
        foreach (var entry in parseResult.Entries)
        {
            var resolution = _addressResolver.ResolveAddress(entry.Address);
            resolvedFrames.Add(new ResolvedStackFrame
            {
                OriginalEntry = entry,
                Resolution = resolution
            });
        }
        
        return new UnityStackTraceResult
        {
            ParseResult = parseResult,
            ResolvedFrames = resolvedFrames
        };
    }
    
    public void Dispose()
    {
        _addressResolver?.Dispose();
    }
}

/// <summary>
/// 解析的堆栈帧
/// </summary>
public struct ResolvedStackFrame
{
    public UnityStackTraceParser.StackTraceEntry OriginalEntry { get; init; }
    public AddressResolutionResult? Resolution { get; init; }
}

/// <summary>
/// Unity 堆栈跟踪解析结果
/// </summary>
public struct UnityStackTraceResult
{
    public UnityStackTraceParser.ParseResult ParseResult { get; init; }
    public List<ResolvedStackFrame> ResolvedFrames { get; init; }
}