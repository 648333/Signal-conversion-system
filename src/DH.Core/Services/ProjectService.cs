using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Serialization;
using DH.Core.Models;

namespace DH.Core.Services;

public sealed class ProjectService : INotifyPropertyChanged
{
    private ProjectInfo? _currentProject;
    private string _projectDirectory = string.Empty;

    public ProjectInfo? CurrentProject
    {
        get => _currentProject;
        private set
        {
            if (SetField(ref _currentProject, value))
                OnPropertyChanged(nameof(IsProjectOpen));
        }
    }

    public bool IsProjectOpen => _currentProject != null;
    public string ProjectDirectory => _projectDirectory;

    public ProjectInfo NewProject(string name, string directory)
    {
        _projectDirectory = directory;
        if (!System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);

        var project = new ProjectInfo
        {
            Name = name,
            FilePath = Path.Combine(directory, name + ".dhproj"),
            Description = "新建工程"
        };

        CurrentProject = project;
        return project;
    }

    public ProjectInfo? LoadProject(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var serializer = new XmlSerializer(typeof(ProjectInfo));
        using var reader = new StreamReader(filePath);
        var project = serializer.Deserialize(reader) as ProjectInfo;
        if (project != null)
        {
            project.FilePath = filePath;
            _projectDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;
            CurrentProject = project;
        }
        return project;
    }

    public bool SaveProject()
    {
        if (_currentProject == null || string.IsNullOrEmpty(_currentProject.FilePath))
            return false;

        return SaveProjectAs(_currentProject.FilePath);
    }

    public bool SaveProjectAs(string filePath)
    {
        if (_currentProject == null)
            return false;

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var serializer = new XmlSerializer(typeof(ProjectInfo));
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new System.Text.UTF8Encoding(false)
        };
        using var writer = XmlWriter.Create(filePath, settings);
        serializer.Serialize(writer, _currentProject);

        _currentProject.FilePath = filePath;
        _projectDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;

        return true;
    }

    public void CloseProject()
    {
        CurrentProject = null;
        _projectDirectory = string.Empty;
    }

    public string GetDataDirectory()
    {
        var path = Path.Combine(_projectDirectory, "Data");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    public string GetConfigDirectory()
    {
        var path = Path.Combine(_projectDirectory, "Config");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
