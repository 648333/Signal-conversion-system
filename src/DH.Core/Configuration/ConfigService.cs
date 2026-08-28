using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace DH.Core.Configuration;

/// <summary>
/// 全局配置服务，管理XML和INI配置文件的读写
/// </summary>
public sealed class ConfigService
{
    private readonly string _configRoot;
    private readonly Dictionary<string, IniFile> _iniCache = new(StringComparer.OrdinalIgnoreCase);

    public ConfigService(string configRoot)
    {
        _configRoot = configRoot;
        if (!Directory.Exists(_configRoot))
            Directory.CreateDirectory(_configRoot);
    }

    public string ConfigRoot => _configRoot;

    /// <summary>
    /// 获取或创建INI配置文件实例（带缓存）
    /// </summary>
    public IniFile GetIniFile(string relativePath)
    {
        var fullPath = Path.Combine(_configRoot, relativePath);
        if (_iniCache.TryGetValue(fullPath, out var ini))
        {
            ini.Load();
            return ini;
        }
        var file = new IniFile(fullPath);
        _iniCache[fullPath] = file;
        return file;
    }

    /// <summary>
    /// 读取XML配置并反序列化为对象
    /// </summary>
    public T? LoadXml<T>(string relativePath) where T : class
    {
        var fullPath = Path.Combine(_configRoot, relativePath);
        if (!File.Exists(fullPath))
            return null;

        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(fullPath);
        return serializer.Deserialize(reader) as T;
    }

    /// <summary>
    /// 将对象序列化为XML并保存
    /// </summary>
    public void SaveXml<T>(string relativePath, T data) where T : class
    {
        var fullPath = Path.Combine(_configRoot, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var serializer = new XmlSerializer(typeof(T));
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new System.Text.UTF8Encoding(false)
        };
        using var writer = XmlWriter.Create(fullPath, settings);
        serializer.Serialize(writer, data);
    }

    /// <summary>
    /// 读取原始XML文档
    /// </summary>
    public XmlDocument? LoadXmlDocument(string relativePath)
    {
        var fullPath = Path.Combine(_configRoot, relativePath);
        if (!File.Exists(fullPath))
            return null;
        var doc = new XmlDocument();
        doc.Load(fullPath);
        return doc;
    }

    /// <summary>
    /// 获取配置文件完整路径
    /// </summary>
    public string GetFullPath(string relativePath) => Path.Combine(_configRoot, relativePath);
}
