using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using ApexHMI.Interfaces;

namespace ApexHMI.Services;

/// <summary>
/// Provides runtime UI localization using .resx resource files.
/// Supports dynamic culture switching with <see cref="CultureChanged"/> event.
/// </summary>
public sealed class LocalizationService : ILocalizationService, INotifyPropertyChanged
{
    private readonly Dictionary<string, ResourceManager> _resourceManagers;
    private string _currentCulture;
    private ResourceManager? _currentManager;

    private static readonly string[] Supported = { "zh-CN", "en-US" };

    public LocalizationService()
    {
        var assembly = typeof(LocalizationService).Assembly;
        _resourceManagers = new Dictionary<string, ResourceManager>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = new ResourceManager("ApexHMI.Properties.Resources_zh-CN", assembly),
            ["en-US"] = new ResourceManager("ApexHMI.Properties.Resources", assembly),
        };

        // Default to zh-CN (the app's original language)
        _currentCulture = "zh-CN";
        _currentManager = _resourceManagers["zh-CN"];

        // Apply to current thread
        ApplyCultureToThread(_currentCulture);
    }

    /// <inheritdoc />
    public string CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (!string.Equals(_currentCulture, value, StringComparison.OrdinalIgnoreCase))
            {
                if (!_resourceManagers.ContainsKey(value))
                    throw new ArgumentException($"Unsupported culture: {value}. Supported: {string.Join(", ", Supported)}");

                _currentCulture = value;
                _currentManager = _resourceManagers[value];
                ApplyCultureToThread(value);
                OnPropertyChanged();
                CultureChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? CultureChanged;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedCultures => Supported;

    /// <inheritdoc />
    public string GetString(string key)
    {
        if (_currentManager is null)
            return $"#{key}#";

        try
        {
            var value = _currentManager.GetString(key);
            if (value is not null)
                return value;

            // Fallback to neutral (en-US)
            value = _resourceManagers["en-US"].GetString(key);
            return value ?? $"#{key}#";
        }
        catch
        {
            return $"#{key}#";
        }
    }

    private static void ApplyCultureToThread(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
