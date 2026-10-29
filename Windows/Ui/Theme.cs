using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Guarana.Windows.Ui;

internal sealed record Palette(
    bool Dark,
    Color Background,
    Color Surface,
    Color SurfaceAlt,
    Color Border,
    Color Text,
    Color TextMuted,
    Color Accent,
    Color AccentHover,
    Color AccentPressed,
    Color OnAccent,
    Color AccentSoft,
    Color Success,
    Color SuccessSoft,
    Color Danger,
    Color Track);

internal static class Theme
{
    // Paleta inspirada na fruta: vermelho da casca, creme do arilo, quase-preto da semente.
    private static readonly Palette Light = new(
        Dark: false,
        Background: Hex("#F6F2EC"),
        Surface: Hex("#FFFFFF"),
        SurfaceAlt: Hex("#F3EEE7"),
        Border: Hex("#E7DFD4"),
        Text: Hex("#1E1916"),
        TextMuted: Hex("#7A6F67"),
        Accent: Hex("#D2382C"),
        AccentHover: Hex("#BC2F24"),
        AccentPressed: Hex("#A1271E"),
        OnAccent: Color.White,
        AccentSoft: Hex("#FBE5E1"),
        Success: Hex("#2E9A58"),
        SuccessSoft: Hex("#E3F4E9"),
        Danger: Hex("#C6362B"),
        Track: Hex("#D8CFC4"));

    private static readonly Palette DarkPalette = new(
        Dark: true,
        Background: Hex("#151312"),
        Surface: Hex("#1F1C1A"),
        SurfaceAlt: Hex("#2A2623"),
        Border: Hex("#342F2B"),
        Text: Hex("#F4EFEA"),
        TextMuted: Hex("#A59A91"),
        Accent: Hex("#F0543A"),
        AccentHover: Hex("#FF6A4F"),
        AccentPressed: Hex("#D9452D"),
        OnAccent: Color.White,
        AccentSoft: Hex("#3A2420"),
        Success: Hex("#4CC27A"),
        SuccessSoft: Hex("#1E3326"),
        Danger: Hex("#FF6B5B"),
        Track: Hex("#4A433E"));

    public static Palette Current { get; private set; } = Detect();

    public static event EventHandler? Changed;

    static Theme()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;
            var detected = Detect();
            if (detected == Current) return;
            Current = detected;
            Changed?.Invoke(null, EventArgs.Empty);
        };
    }

    private static Palette Detect()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0 ? DarkPalette : Light;
        }
        catch
        {
            return Light;
        }
    }

    // ---- Typography ----------------------------------------------------------------

    private static readonly string FamilyName = PickFamily("Segoe UI Variable Text", "Segoe UI");
    private static readonly string DisplayFamilyName = PickFamily("Segoe UI Variable Display", "Segoe UI Semibold", "Segoe UI");
    private static readonly Dictionary<(string, float, FontStyle), Font> Fonts = [];

    private static string PickFamily(params string[] candidates)
    {
        using var installed = new System.Drawing.Text.InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return candidates.FirstOrDefault(names.Contains) ?? SystemFonts.MessageBoxFont?.Name ?? "Segoe UI";
    }

    public static Font Body => Get(FamilyName, 9.75f);
    public static Font BodyStrong => Get(DisplayFamilyName, 9.75f, FontStyle.Bold);
    public static Font Small => Get(FamilyName, 8.75f);
    public static Font SectionTitle => Get(DisplayFamilyName, 11.5f, FontStyle.Bold);
    public static Font Title => Get(DisplayFamilyName, 17f, FontStyle.Bold);
    public static Font Hero => Get(DisplayFamilyName, 15f, FontStyle.Bold);

    private static Font Get(string family, float size, FontStyle style = FontStyle.Regular)
    {
        var key = (family, size, style);
        if (!Fonts.TryGetValue(key, out var font))
            Fonts[key] = font = new Font(family, size, style, GraphicsUnit.Point);
        return font;
    }

    // ---- Window chrome --------------------------------------------------------------

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCaptionColor = 35;

    /// <summary>Barra de título escura/clara e cantos arredondados (Windows 11). Ignorado onde não houver suporte.</summary>
    public static void ApplyChrome(Form form)
    {
        if (!form.IsHandleCreated) return;
        var p = Current;
        int dark = p.Dark ? 1 : 0;
        int round = 2;
        int caption = ColorTranslator.ToWin32(p.Background);
        DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        DwmSetWindowAttribute(form.Handle, DwmWindowCornerPreference, ref round, sizeof(int));
        DwmSetWindowAttribute(form.Handle, DwmCaptionColor, ref caption, sizeof(int));
    }

    // ---- Drawing helpers ------------------------------------------------------------

    public static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    public static Color Blend(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>A cor efetivamente visível atrás de um controle, ignorando fundos transparentes.</summary>
    public static Color BackdropOf(Control control)
    {
        for (var c = control.Parent; c is not null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return Current.Background;
    }

    public static void Prepare(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }
}
