namespace ProjectTimer.Services;

public enum ColorScheme
{
    Lavendel,
    Minze,
    Pfirsich
}

public sealed class ThemeService
{
    private const string ColorSchemeKey = "color_scheme";
    private const string DarkModeKey = "dark_mode";

    private static readonly IReadOnlyDictionary<ColorScheme, Palette> Palettes = new Dictionary<ColorScheme, Palette>
    {
        [ColorScheme.Lavendel] = new("Lavendel", "Sanft & konzentriert", "#7C5FB2", "#E9DDFF", "#5E477F", "#4C3764"),
        [ColorScheme.Minze] = new("Minze", "Ruhig & frisch", "#386A5A", "#B8F2D3", "#1F4E40", "#1E4C3E"),
        [ColorScheme.Pfirsich] = new("Pfirsich", "Warm & freundlich", "#9A4930", "#FFDCCF", "#742E1B", "#66321F")
    };

    public ColorScheme SelectedScheme { get; private set; } = ColorScheme.Lavendel;
    public bool IsDarkMode { get; private set; }

    public event EventHandler? ThemeChanged;

    public void Initialize()
    {
        SelectedScheme = (ColorScheme)Preferences.Default.Get(ColorSchemeKey, (int)ColorScheme.Lavendel);
        IsDarkMode = Preferences.Default.Get(DarkModeKey, false);
        Apply();
    }

    public void SetScheme(ColorScheme scheme)
    {
        if (SelectedScheme == scheme)
        {
            return;
        }

        SelectedScheme = scheme;
        Preferences.Default.Set(ColorSchemeKey, (int)scheme);
        Apply();
    }

    public void SetDarkMode(bool isDarkMode)
    {
        if (IsDarkMode == isDarkMode)
        {
            return;
        }

        IsDarkMode = isDarkMode;
        Preferences.Default.Set(DarkModeKey, isDarkMode);
        Apply();
    }

    public ThemeOption CreateOption(ColorScheme scheme) => new(scheme, Palettes[scheme]);

    private void Apply()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.UserAppTheme = IsDarkMode ? AppTheme.Dark : AppTheme.Light;
        var palette = Palettes[SelectedScheme];
        var colors = IsDarkMode ? palette.DarkColors : palette.LightColors;
        foreach (var (key, value) in colors)
        {
            app.Resources[key] = Color.FromArgb(value);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public sealed class ThemeOption(ColorScheme scheme, Palette palette)
    {
        public ColorScheme Scheme { get; } = scheme;
        public string Name { get; } = palette.Name;
        public string Description { get; } = palette.Description;
        public Color PreviewPrimary { get; } = Color.FromArgb(palette.PreviewPrimary);
        public Color PreviewContainer { get; } = Color.FromArgb(palette.PreviewContainer);
    }

    public sealed class Palette(string name, string description, string previewPrimary, string previewContainer, string darkPrimary, string darkPrimaryContainer)
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public string PreviewPrimary { get; } = previewPrimary;
        public string PreviewContainer { get; } = previewContainer;

        public IReadOnlyDictionary<string, string> LightColors { get; } = new Dictionary<string, string>
        {
            ["Primary"] = previewPrimary,
            ["OnPrimary"] = "#FFFFFF",
            ["PrimaryContainer"] = previewContainer,
            ["OnPrimaryContainer"] = "#241A32",
            ["SecondaryContainer"] = "#E9E1EA",
            ["OnSecondaryContainer"] = "#1F1A20",
            ["Surface"] = "#FFFBFF",
            ["SurfaceContainer"] = "#F5EFF7",
            ["SurfaceContainerHigh"] = "#EDE7EF",
            ["OnSurface"] = "#1D1B20",
            ["OnSurfaceVariant"] = "#4C444B",
            ["Outline"] = "#7D747C",
            ["OutlineVariant"] = "#D0C4CD",
            ["Success"] = "#176B45",
            ["Error"] = "#BA1A1A",
            ["OnError"] = "#FFFFFF",
            ["ErrorContainer"] = "#FFDAD6",
            ["OnErrorContainer"] = "#410002"
        };

        public IReadOnlyDictionary<string, string> DarkColors { get; } = new Dictionary<string, string>
        {
            ["Primary"] = darkPrimary,
            ["OnPrimary"] = "#FFFFFF",
            ["PrimaryContainer"] = darkPrimaryContainer,
            ["OnPrimaryContainer"] = "#E9DDFF",
            ["SecondaryContainer"] = darkPrimaryContainer,
            ["OnSecondaryContainer"] = "#E8E0E8",
            ["Surface"] = "#151217",
            ["SurfaceContainer"] = "#211E23",
            ["SurfaceContainerHigh"] = "#2B282D",
            ["OnSurface"] = "#E8E0E8",
            ["OnSurfaceVariant"] = "#D1C5CD",
            ["Outline"] = "#9B9098",
            ["OutlineVariant"] = "#4C444B",
            ["Success"] = "#72D6A8",
            ["Error"] = "#FFB4AB",
            ["OnError"] = "#690005",
            ["ErrorContainer"] = "#93000A",
            ["OnErrorContainer"] = "#FFDAD6"
        };
    }
}
