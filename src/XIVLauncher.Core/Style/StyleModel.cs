using Newtonsoft.Json;

namespace XIVLauncher.Core.Style;

/// <summary>
/// Superclass for all versions of the Dalamud style model.
/// </summary>
public abstract class StyleModel
{
    /// <summary>
    /// Gets or sets the name of the style model.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = "Unknown";

    /// <summary>
    /// Gets or sets version number of this model.
    /// </summary>
    [JsonProperty("ver")]
    public int Version { get; set; }

    /// <summary>
    /// Get a StyleModel based on the current Dalamud style, with the current version.
    /// </summary>
    /// <returns>The current style.</returns>
    public static StyleModel GetFromCurrent() => StyleModelV1.Get();

    /// <summary>
    /// Apply this style model to ImGui.
    /// </summary>
    public abstract void Apply();

    /// <summary>
    /// Push this StyleModel into the ImGui style/color stack.
    /// </summary>
    public abstract void Push();

    /// <summary>
    /// Pop this style model from the ImGui style/color stack.
    /// </summary>
    public abstract void Pop();
}