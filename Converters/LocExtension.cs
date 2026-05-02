using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using ApexHMI.Interfaces;

namespace ApexHMI.Converters;

/// <summary>
/// XAML markup extension that resolves localized strings from <see cref="ILocalizationService"/>.
/// Usage in XAML:
///   Text="{l:Loc Monitor_DetailedData}"
///   
/// Automatically updates when culture changes via <see cref="ILocalizationService.CultureChanged"/>.
/// </summary>
public class LocExtension : MarkupExtension
{
    private static ILocalizationService? _localizationService;
    private static readonly HashSet<LocWatcher> ActiveWatchers = new();

    /// <summary>
    /// Initializes the shared <see cref="ILocalizationService"/> instance.
    /// Must be called once at startup (e.g., from Bootstrapper or App).
    /// </summary>
    public static void Initialize(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;
    }

    /// <summary>
    /// Parameterless constructor required by XAML.
    /// </summary>
    public LocExtension() { }

    /// <summary>
    /// Constructor accepting the resource key.
    /// Usage in XAML: <c>{l:Loc SomeKey}</c>
    /// </summary>
    public LocExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// The resource key to look up.
    /// </summary>
    [ConstructorArgument("key")]
    public string? Key { get; set; }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
            && target.TargetObject is DependencyObject depObj
            && target.TargetProperty is DependencyProperty depProp)
        {
            var watcher = new LocWatcher(depObj, depProp, Key);
            lock (ActiveWatchers)
            {
                ActiveWatchers.Add(watcher);
            }
            return Resolve();
        }

        // Design-time fallback
        return Resolve();
    }

    private string Resolve()
    {
        if (_localizationService is null)
            return Key ?? string.Empty;

        return _localizationService.GetString(Key!);
    }

    private static void OnCultureChanged(object? sender, EventArgs e)
    {
        LocWatcher[] watchers;
        lock (ActiveWatchers)
        {
            watchers = ActiveWatchers.ToArray();
        }

        foreach (var watcher in watchers)
        {
            watcher.Refresh();
        }
    }

    /// <summary>
    /// Tracks a dependency property binding to a localization key.
    /// </summary>
    private sealed class LocWatcher
    {
        private readonly WeakReference<DependencyObject> _targetRef;
        private readonly DependencyProperty _property;
        private readonly string _key;

        public LocWatcher(DependencyObject target, DependencyProperty property, string key)
        {
            _targetRef = new WeakReference<DependencyObject>(target);
            _property = property;
            _key = key;
        }

        public void Refresh()
        {
            if (_localizationService is null) return;

            if (_targetRef.TryGetTarget(out var target))
            {
                var value = _localizationService.GetString(_key);
                target.SetValue(_property, value);
            }
            else
            {
                lock (ActiveWatchers)
                {
                    ActiveWatchers.Remove(this);
                }
            }
        }
    }
}
