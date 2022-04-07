using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;

namespace XIVLauncher.Core.Style;

/// <summary>
/// Version one of the Dalamud style model.
/// </summary>
public class StyleModelV1 : StyleModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StyleModelV1"/> class.
    /// </summary>
    private StyleModelV1()
    {
        this.Colors = new Dictionary<string, Vector4>();
        this.Name = "Unknown";
    }

    /// <summary>
    /// Gets the standard Dalamud look.
    /// </summary>
    public static StyleModelV1 DalamudStandard => new()
    {
        Name = "Dalamud Standard",

        Alpha = 1,
        WindowPadding = new Vector2(8, 8),
        WindowRounding = 4,
        WindowBorderSize = 0,
        WindowTitleAlign = new Vector2(0, 0.5f),
        WindowMenuButtonPosition = ImGuiDir.Right,
        ChildRounding = 0,
        ChildBorderSize = 1,
        PopupRounding = 0,
        PopupBorderSize = 0,
        FramePadding = new Vector2(4, 3),
        //FrameRounding = 4,
        FrameRounding = 0,
        FrameBorderSize = 0,
        ItemSpacing = new Vector2(8, 4),
        ItemInnerSpacing = new Vector2(4, 4),
        CellPadding = new Vector2(4, 2),
        TouchExtraPadding = new Vector2(0, 0),
        IndentSpacing = 21,
        ScrollbarSize = 16,
        ScrollbarRounding = 9,
        GrabMinSize = 13,
        GrabRounding = 3,
        LogSliderDeadzone = 4,
        TabRounding = 1,
        TabBorderSize = 0,
        ButtonTextAlign = new Vector2(0.5f, 0.5f),
        SelectableTextAlign = new Vector2(0, 0),
        DisplaySafeAreaPadding = new Vector2(3, 3),

        Colors = new Dictionary<string, Vector4>
        {
            { "Text", new Vector4(1, 1, 1, 1) },
            { "TextDisabled", new Vector4(0.5f, 0.5f, 0.5f, 1) },
            { "WindowBg", new Vector4(0.06f, 0.06f, 0.06f, 0.93f) },
            { "ChildBg", new Vector4(0, 0, 0, 0) },
            { "PopupBg", new Vector4(0.08f, 0.08f, 0.08f, 0.94f) },
            { "Border", new Vector4(0.43f, 0.43f, 0.5f, 0.5f) },
            { "BorderShadow", new Vector4(0, 0, 0, 0) },
            { "FrameBg", new Vector4(0.29f, 0.29f, 0.29f, 0.54f) },
            { "FrameBgHovered", new Vector4(0.54f, 0.54f, 0.54f, 0.4f) },
            { "FrameBgActive", new Vector4(0.64f, 0.64f, 0.64f, 0.67f) },
            { "TitleBg", new Vector4(0.022624433f, 0.022624206f, 0.022624206f, 0.85067874f) },
            { "TitleBgActive", new Vector4(0.38914025f, 0.10917056f, 0.10917056f, 0.8280543f) },
            { "TitleBgCollapsed", new Vector4(0, 0, 0, 0.51f) },
            { "MenuBarBg", new Vector4(0.14f, 0.14f, 0.14f, 1) },
            { "ScrollbarBg", new Vector4(0, 0, 0, 0) },
            { "ScrollbarGrab", new Vector4(0.31f, 0.31f, 0.31f, 1) },
            { "ScrollbarGrabHovered", new Vector4(0.41f, 0.41f, 0.41f, 1) },
            { "ScrollbarGrabActive", new Vector4(0.51f, 0.51f, 0.51f, 1) },
            { "CheckMark", new Vector4(0.86f, 0.86f, 0.86f, 1) },
            { "SliderGrab", new Vector4(0.54f, 0.54f, 0.54f, 1) },
            { "SliderGrabActive", new Vector4(0.67f, 0.67f, 0.67f, 1) },
            { "Button", new Vector4(0.71f, 0.71f, 0.71f, 0.4f) },
            { "ButtonHovered", ImGuiColors.BlueShade3 },
            { "ButtonActive", ImGuiColors.Blue },
            { "Header", new Vector4(0.59f, 0.59f, 0.59f, 0.31f) },
            { "HeaderHovered", new Vector4(0.5f, 0.5f, 0.5f, 0.8f) },
            { "HeaderActive", new Vector4(0.6f, 0.6f, 0.6f, 1) },
            { "Separator", new Vector4(0.43f, 0.43f, 0.5f, 0.5f) },
            { "SeparatorHovered", new Vector4(0.3647059f, 0.078431375f, 0.078431375f, 0.78280544f) },
            { "SeparatorActive", new Vector4(0.3647059f, 0.078431375f, 0.078431375f, 0.94509804f) },
            { "ResizeGrip", new Vector4(0.79f, 0.79f, 0.79f, 0.25f) },
            { "ResizeGripHovered", new Vector4(0.78f, 0.78f, 0.78f, 0.67f) },
            { "ResizeGripActive", new Vector4(0.3647059f, 0.078431375f, 0.078431375f, 0.94509804f) },
            { "Tab", new Vector4(0.23f, 0.23f, 0.23f, 0.86f) },
            { "TabHovered", ImGuiColors.BlueShade3 },
            { "TabActive", ImGuiColors.Blue },
            { "TabUnfocused", new Vector4(0.068f, 0.10199998f, 0.14800003f, 0.9724f) },
            { "TabUnfocusedActive", ImGuiColors.Blue },
            { "DockingPreview", new Vector4(0.26f, 0.59f, 0.98f, 0.7f) },
            { "DockingEmptyBg", new Vector4(0.2f, 0.2f, 0.2f, 1) },
            { "PlotLines", new Vector4(0.61f, 0.61f, 0.61f, 1) },
            { "PlotLinesHovered", new Vector4(1, 0.43f, 0.35f, 1) },
            { "PlotHistogram", new Vector4(0.9f, 0.7f, 0, 1) },
            { "PlotHistogramHovered", new Vector4(1, 0.6f, 0, 1) },
            { "TableHeaderBg", new Vector4(0.19f, 0.19f, 0.2f, 1) },
            { "TableBorderStrong", new Vector4(0.31f, 0.31f, 0.35f, 1) },
            { "TableBorderLight", new Vector4(0.23f, 0.23f, 0.25f, 1) },
            { "TableRowBg", new Vector4(0, 0, 0, 0) },
            { "TableRowBgAlt", new Vector4(1, 1, 1, 0.06f) },
            { "TextSelectedBg", new Vector4(0.26f, 0.59f, 0.98f, 0.35f) },
            { "DragDropTarget", new Vector4(1, 1, 0, 0.9f) },
            { "NavHighlight", new Vector4(0.26f, 0.59f, 0.98f, 1) },
            { "NavWindowingHighlight", new Vector4(1, 1, 1, 0.7f) },
            { "NavWindowingDimBg", new Vector4(0.8f, 0.8f, 0.8f, 0.2f) },
            { "ModalWindowDimBg", new Vector4(0.8f, 0.8f, 0.8f, 0.35f) },
        },
    };

    /// <summary>
    /// Gets the standard Dalamud look.
    /// </summary>
    public static StyleModelV1 DalamudClassic => new()
    {
        Name = "Dalamud Classic",

        Alpha = 1,
        WindowPadding = new Vector2(8, 8),
        WindowRounding = 4,
        WindowBorderSize = 0,
        WindowTitleAlign = new Vector2(0, 0.5f),
        WindowMenuButtonPosition = ImGuiDir.Right,
        ChildRounding = 0,
        ChildBorderSize = 1,
        PopupRounding = 0,
        PopupBorderSize = 0,
        FramePadding = new Vector2(4, 3),
        FrameRounding = 4,
        FrameBorderSize = 0,
        ItemSpacing = new Vector2(8, 4),
        ItemInnerSpacing = new Vector2(4, 4),
        CellPadding = new Vector2(4, 2),
        TouchExtraPadding = new Vector2(0, 0),
        IndentSpacing = 21,
        ScrollbarSize = 16,
        ScrollbarRounding = 9,
        GrabMinSize = 10,
        GrabRounding = 3,
        LogSliderDeadzone = 4,
        TabRounding = 4,
        TabBorderSize = 0,
        ButtonTextAlign = new Vector2(0.5f, 0.5f),
        SelectableTextAlign = new Vector2(0, 0),
        DisplaySafeAreaPadding = new Vector2(3, 3),

        Colors = new Dictionary<string, Vector4>
        {
            { "Text", new Vector4(1f, 1f, 1f, 1f) },
            { "TextDisabled", new Vector4(0.5f, 0.5f, 0.5f, 1f) },
            { "WindowBg", new Vector4(0.06f, 0.06f, 0.06f, 0.87f) },
            { "ChildBg", new Vector4(0f, 0f, 0f, 0f) },
            { "PopupBg", new Vector4(0.08f, 0.08f, 0.08f, 0.94f) },
            { "Border", new Vector4(0.43f, 0.43f, 0.5f, 0.5f) },
            { "BorderShadow", new Vector4(0f, 0f, 0f, 0f) },
            { "FrameBg", new Vector4(0.29f, 0.29f, 0.29f, 0.54f) },
            { "FrameBgHovered", new Vector4(0.54f, 0.54f, 0.54f, 0.4f) },
            { "FrameBgActive", new Vector4(0.64f, 0.64f, 0.64f, 0.67f) },
            { "TitleBg", new Vector4(0.04f, 0.04f, 0.04f, 1f) },
            { "TitleBgActive", new Vector4(0.29f, 0.29f, 0.29f, 1f) },
            { "TitleBgCollapsed", new Vector4(0f, 0f, 0f, 0.51f) },
            { "MenuBarBg", new Vector4(0.14f, 0.14f, 0.14f, 1f) },
            { "ScrollbarBg", new Vector4(0f, 0f, 0f, 0f) },
            { "ScrollbarGrab", new Vector4(0.31f, 0.31f, 0.31f, 1f) },
            { "ScrollbarGrabHovered", new Vector4(0.41f, 0.41f, 0.41f, 1f) },
            { "ScrollbarGrabActive", new Vector4(0.51f, 0.51f, 0.51f, 1f) },
            { "CheckMark", new Vector4(0.86f, 0.86f, 0.86f, 1f) },
            { "SliderGrab", new Vector4(0.54f, 0.54f, 0.54f, 1f) },
            { "SliderGrabActive", new Vector4(0.67f, 0.67f, 0.67f, 1f) },
            { "Button", new Vector4(0.71f, 0.71f, 0.71f, 0.4f) },
            { "ButtonHovered", new Vector4(0.47f, 0.47f, 0.47f, 1f) },
            { "ButtonActive", new Vector4(0.74f, 0.74f, 0.74f, 1f) },
            { "Header", new Vector4(0.59f, 0.59f, 0.59f, 0.31f) },
            { "HeaderHovered", new Vector4(0.5f, 0.5f, 0.5f, 0.8f) },
            { "HeaderActive", new Vector4(0.6f, 0.6f, 0.6f, 1f) },
            { "Separator", new Vector4(0.43f, 0.43f, 0.5f, 0.5f) },
            { "SeparatorHovered", new Vector4(0.1f, 0.4f, 0.75f, 0.78f) },
            { "SeparatorActive", new Vector4(0.1f, 0.4f, 0.75f, 1f) },
            { "ResizeGrip", new Vector4(0.79f, 0.79f, 0.79f, 0.25f) },
            { "ResizeGripHovered", new Vector4(0.78f, 0.78f, 0.78f, 0.67f) },
            { "ResizeGripActive", new Vector4(0.88f, 0.88f, 0.88f, 0.95f) },
            { "Tab", new Vector4(0.23f, 0.23f, 0.23f, 0.86f) },
            { "TabHovered", new Vector4(0.71f, 0.71f, 0.71f, 0.8f) },
            { "TabActive", new Vector4(0.36f, 0.36f, 0.36f, 1f) },
            { "TabUnfocused", new Vector4(0.068f, 0.10199998f, 0.14800003f, 0.9724f) },
            { "TabUnfocusedActive", new Vector4(0.13599998f, 0.26199996f, 0.424f, 1f) },
            { "DockingPreview", new Vector4(0.26f, 0.59f, 0.98f, 0.7f) },
            { "DockingEmptyBg", new Vector4(0.2f, 0.2f, 0.2f, 1f) },
            { "PlotLines", new Vector4(0.61f, 0.61f, 0.61f, 1f) },
            { "PlotLinesHovered", new Vector4(1f, 0.43f, 0.35f, 1f) },
            { "PlotHistogram", new Vector4(0.9f, 0.7f, 0f, 1f) },
            { "PlotHistogramHovered", new Vector4(1f, 0.6f, 0f, 1f) },
            { "TableHeaderBg", new Vector4(0.19f, 0.19f, 0.2f, 1f) },
            { "TableBorderStrong", new Vector4(0.31f, 0.31f, 0.35f, 1f) },
            { "TableBorderLight", new Vector4(0.23f, 0.23f, 0.25f, 1f) },
            { "TableRowBg", new Vector4(0f, 0f, 0f, 0f) },
            { "TableRowBgAlt", new Vector4(1f, 1f, 1f, 0.06f) },
            { "TextSelectedBg", new Vector4(0.26f, 0.59f, 0.98f, 0.35f) },
            { "DragDropTarget", new Vector4(1f, 1f, 0f, 0.9f) },
            { "NavHighlight", new Vector4(0.26f, 0.59f, 0.98f, 1f) },
            { "NavWindowingHighlight", new Vector4(1f, 1f, 1f, 0.7f) },
            { "NavWindowingDimBg", new Vector4(0.8f, 0.8f, 0.8f, 0.2f) },
            { "ModalWindowDimBg", new Vector4(0.8f, 0.8f, 0.8f, 0.35f) },
        },
    };

    /// <summary>
    /// Gets the version prefix for this version.
    /// </summary>
    public static string SerializedPrefix => "DS1";

#pragma warning disable SA1600

    [JsonProperty("a")]
    public float Alpha { get; set; }

    [JsonProperty("b")]
    public Vector2 WindowPadding { get; set; }

    [JsonProperty("c")]
    public float WindowRounding { get; set; }

    [JsonProperty("d")]
    public float WindowBorderSize { get; set; }

    [JsonProperty("e")]
    public Vector2 WindowTitleAlign { get; set; }

    [JsonProperty("f")]
    public ImGuiDir WindowMenuButtonPosition { get; set; }

    [JsonProperty("g")]
    public float ChildRounding { get; set; }

    [JsonProperty("h")]
    public float ChildBorderSize { get; set; }

    [JsonProperty("i")]
    public float PopupRounding { get; set; }

    [JsonProperty("ab")]
    public float PopupBorderSize { get; set; }

    [JsonProperty("j")]
    public Vector2 FramePadding { get; set; }

    [JsonProperty("k")]
    public float FrameRounding { get; set; }

    [JsonProperty("l")]
    public float FrameBorderSize { get; set; }

    [JsonProperty("m")]
    public Vector2 ItemSpacing { get; set; }

    [JsonProperty("n")]
    public Vector2 ItemInnerSpacing { get; set; }

    [JsonProperty("o")]
    public Vector2 CellPadding { get; set; }

    [JsonProperty("p")]
    public Vector2 TouchExtraPadding { get; set; }

    [JsonProperty("q")]
    public float IndentSpacing { get; set; }

    [JsonProperty("r")]
    public float ScrollbarSize { get; set; }

    [JsonProperty("s")]
    public float ScrollbarRounding { get; set; }

    [JsonProperty("t")]
    public float GrabMinSize { get; set; }

    [JsonProperty("u")]
    public float GrabRounding { get; set; }

    [JsonProperty("v")]
    public float LogSliderDeadzone { get; set; }

    [JsonProperty("w")]
    public float TabRounding { get; set; }

    [JsonProperty("x")]
    public float TabBorderSize { get; set; }

    [JsonProperty("y")]
    public Vector2 ButtonTextAlign { get; set; }

    [JsonProperty("z")]
    public Vector2 SelectableTextAlign { get; set; }

    [JsonProperty("aa")]
    public Vector2 DisplaySafeAreaPadding { get; set; }

#pragma warning restore SA1600

    /// <summary>
    /// Gets or sets a dictionary mapping ImGui color names to colors.
    /// </summary>
    [JsonProperty("col")]
    public Dictionary<string, Vector4> Colors { get; set; }

    /// <summary>
    /// Get a <see cref="StyleModel"/> instance via ImGui.
    /// </summary>
    /// <returns>The newly created <see cref="StyleModel"/> instance.</returns>
    public static StyleModelV1 Get()
    {
        var model = new StyleModelV1();
        var style = ImGui.GetStyle();

        model.Alpha = style.Alpha;
        model.WindowPadding = style.WindowPadding;
        model.WindowRounding = style.WindowRounding;
        model.WindowBorderSize = style.WindowBorderSize;
        model.WindowTitleAlign = style.WindowTitleAlign;
        model.WindowMenuButtonPosition = style.WindowMenuButtonPosition;
        model.ChildRounding = style.ChildRounding;
        model.ChildBorderSize = style.ChildBorderSize;
        model.PopupRounding = style.PopupRounding;
        model.PopupBorderSize = style.PopupBorderSize;
        model.FramePadding = style.FramePadding;
        model.FrameRounding = style.FrameRounding;
        model.FrameBorderSize = style.FrameBorderSize;
        model.ItemSpacing = style.ItemSpacing;
        model.ItemInnerSpacing = style.ItemInnerSpacing;
        model.CellPadding = style.CellPadding;
        model.TouchExtraPadding = style.TouchExtraPadding;
        model.IndentSpacing = style.IndentSpacing;
        model.ScrollbarSize = style.ScrollbarSize;
        model.ScrollbarRounding = style.ScrollbarRounding;
        model.GrabMinSize = style.GrabMinSize;
        model.GrabRounding = style.GrabRounding;
        model.LogSliderDeadzone = style.LogSliderDeadzone;
        model.TabRounding = style.TabRounding;
        model.TabBorderSize = style.TabBorderSize;
        model.ButtonTextAlign = style.ButtonTextAlign;
        model.SelectableTextAlign = style.SelectableTextAlign;
        model.DisplaySafeAreaPadding = style.DisplaySafeAreaPadding;

        model.Colors = new Dictionary<string, Vector4>();

        foreach (var imGuiCol in Enum.GetValues<ImGuiCol>())
        {
            if (imGuiCol == ImGuiCol.COUNT)
            {
                continue;
            }

            model.Colors[imGuiCol.ToString()] = style.Colors[(int)imGuiCol];
        }

        return model;
    }

    /// <summary>
    /// Apply this StyleModel via ImGui.
    /// </summary>
    public override void Apply()
    {
        var style = ImGui.GetStyle();

        style.Alpha = this.Alpha;
        style.WindowPadding = this.WindowPadding;
        style.WindowRounding = this.WindowRounding;
        style.WindowBorderSize = this.WindowBorderSize;
        style.WindowTitleAlign = this.WindowTitleAlign;
        style.WindowMenuButtonPosition = this.WindowMenuButtonPosition;
        style.ChildRounding = this.ChildRounding;
        style.ChildBorderSize = this.ChildBorderSize;
        style.PopupRounding = this.PopupRounding;
        style.PopupBorderSize = this.PopupBorderSize;
        style.FramePadding = this.FramePadding;
        style.FrameRounding = this.FrameRounding;
        style.FrameBorderSize = this.FrameBorderSize;
        style.ItemSpacing = this.ItemSpacing;
        style.ItemInnerSpacing = this.ItemInnerSpacing;
        style.CellPadding = this.CellPadding;
        style.TouchExtraPadding = this.TouchExtraPadding;
        style.IndentSpacing = this.IndentSpacing;
        style.ScrollbarSize = this.ScrollbarSize;
        style.ScrollbarRounding = this.ScrollbarRounding;
        style.GrabMinSize = this.GrabMinSize;
        style.GrabRounding = this.GrabRounding;
        style.LogSliderDeadzone = this.LogSliderDeadzone;
        style.TabRounding = this.TabRounding;
        style.TabBorderSize = this.TabBorderSize;
        style.ButtonTextAlign = this.ButtonTextAlign;
        style.SelectableTextAlign = this.SelectableTextAlign;
        style.DisplaySafeAreaPadding = this.DisplaySafeAreaPadding;

        foreach (var imGuiCol in Enum.GetValues<ImGuiCol>())
        {
            if (imGuiCol == ImGuiCol.COUNT)
            {
                continue;
            }

            style.Colors[(int)imGuiCol] = this.Colors[imGuiCol.ToString()];
        }
    }

    /// <inheritdoc/>
    public override void Push()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override void Pop()
    {
        throw new NotImplementedException();
    }
}