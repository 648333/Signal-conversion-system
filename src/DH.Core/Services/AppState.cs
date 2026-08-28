using System.ComponentModel;
using System.Runtime.CompilerServices;
using DH.Core.Models;

namespace DH.Core.Services;

/// <summary>
/// 全局应用状态：当前工程、模式、语言等
/// </summary>
public sealed class AppState : INotifyPropertyChanged
{
    private ProjectInfo? _currentProject;
    private string _currentLanguage = "zh-CN";
    private bool _isMeasureMode = true;
    private string _currentSoftwarePackage = "CommonSoft";

    public ProjectInfo? CurrentProject
    {
        get => _currentProject;
        set => SetField(ref _currentProject, value);
    }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetField(ref _currentLanguage, value);
    }

    public bool IsMeasureMode
    {
        get => _isMeasureMode;
        set => SetField(ref _isMeasureMode, value);
    }

    public string CurrentSoftwarePackage
    {
        get => _currentSoftwarePackage;
        set => SetField(ref _currentSoftwarePackage, value);
    }

    public bool IsAnalysisMode => !_isMeasureMode;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
