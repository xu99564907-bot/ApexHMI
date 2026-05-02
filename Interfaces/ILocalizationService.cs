namespace ApexHMI.Interfaces;

/// <summary>
/// Manages UI culture switching for runtime localization.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets or sets the current UI culture (e.g., "zh-CN", "en-US").
    /// Raises <see cref="CultureChanged"/> when changed.
    /// </summary>
    string CurrentCulture { get; set; }

    /// <summary>
    /// Event raised whenever the UI culture changes.
    /// Subscribers (e.g., LocExtension) update their displayed values.
    /// </summary>
    event EventHandler? CultureChanged;

    /// <summary>
    /// Retrieves the localized string for the given resource key.
    /// Falls back to the neutral resource if the key is not found in the current culture.
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Returns the supported culture names in display order.
    /// </summary>
    IReadOnlyList<string> SupportedCultures { get; }
}
