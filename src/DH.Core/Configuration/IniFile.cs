using System.IO;
using System.Text;

namespace DH.Core.Configuration;

/// <summary>
/// INI配置文件读写器，兼容东华DHDAS的INI格式
/// </summary>
public sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _filePath;

    public IniFile(string filePath)
    {
        _filePath = filePath;
        if (File.Exists(filePath))
            Load();
    }

    public void Load()
    {
        _sections.Clear();
        var lines = File.ReadAllLines(_filePath, Encoding.Default);
        var currentSection = string.Empty;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                continue;

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                currentSection = trimmed[1..^1];
                if (!_sections.ContainsKey(currentSection))
                    _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else if (trimmed.Contains('='))
            {
                var idx = trimmed.IndexOf('=');
                var key = trimmed[..idx].Trim();
                var value = trimmed[(idx + 1)..].Trim();
                if (!string.IsNullOrEmpty(currentSection))
                    _sections[currentSection][key] = value;
            }
        }
    }

    public void Save()
    {
        var sb = new StringBuilder();
        foreach (var section in _sections)
        {
            sb.AppendLine($"[{section.Key}]");
            foreach (var kv in section.Value)
                sb.AppendLine($"{kv.Key}={kv.Value}");
            sb.AppendLine();
        }
        File.WriteAllText(_filePath, sb.ToString(), Encoding.Default);
    }

    public string Read(string section, string key, string defaultValue = "")
    {
        if (_sections.TryGetValue(section, out var s) && s.TryGetValue(key, out var v))
            return v;
        return defaultValue;
    }

    public int ReadInt(string section, string key, int defaultValue = 0)
    {
        var v = Read(section, key);
        return int.TryParse(v, out var result) ? result : defaultValue;
    }

    public double ReadDouble(string section, string key, double defaultValue = 0)
    {
        var v = Read(section, key);
        return double.TryParse(v, out var result) ? result : defaultValue;
    }

    public bool ReadBool(string section, string key, bool defaultValue = false)
    {
        var v = Read(section, key, "0");
        return v is "1" or "true" or "True";
    }

    public void Write(string section, string key, string value)
    {
        if (!_sections.ContainsKey(section))
            _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _sections[section][key] = value;
    }

    public void WriteInt(string section, string key, int value) => Write(section, key, value.ToString());

    public void WriteBool(string section, string key, bool value) => Write(section, key, value ? "1" : "0");

    public IEnumerable<string> GetSections() => _sections.Keys;

    public IEnumerable<string> GetKeys(string section)
    {
        return _sections.TryGetValue(section, out var s) ? s.Keys : Enumerable.Empty<string>();
    }
}
