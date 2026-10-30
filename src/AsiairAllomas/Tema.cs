using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AsiairAllomas;

public sealed record Szinek(
    Color Eg, Color Eg2, Color Vonal, Color Feny, Color Halk, Color Oiii, Color Hold, Color Ha, Color Felho,
    Color SavNap, Color SavPolgari, Color SavNavig, Color SavCsill, Color SavSotet);

public static class Tema
{
    private static readonly Szinek Normal = new(
        Hex("#0c1230"), Hex("#131a3c"), Hex("#28305a"), Hex("#e6e9f5"), Hex("#9aa2c8"), Hex("#46c2b8"),
        Hex("#e9d59a"), Hex("#e0566b"), Hex("#c9cfe6"),
        Hex("#3a4886"), Hex("#2b3672"), Hex("#1f285c"), Hex("#161e48"), Hex("#0a0f28"));

    private static readonly Szinek Voros = new(
        Hex("#000000"), Hex("#0a0000"), Hex("#2a0607"), Hex("#e8262c"), Hex("#a83034"), Hex("#c22228"),
        Hex("#d23a40"), Hex("#ff4a50"), Hex("#5a1215"),
        Hex("#2a0507"), Hex("#200405"), Hex("#160203"), Hex("#0e0102"), Hex("#000000"));

    private static readonly ColorMatrix VorosMatrix = new(
    [
        [0.30f, 0f, 0f, 0f, 0f],
        [0.59f, 0f, 0f, 0f, 0f],
        [0.11f, 0f, 0f, 0f, 0f],
        [0f, 0f, 0f, 1f, 0f],
        [0f, 0f, 0f, 0f, 1f],
    ]);

    private static readonly Dictionary<(float, string), Font> Betuk = [];
    private static readonly string Alap = ElsoTelepitett("Bahnschrift", "Segoe UI");
    private static readonly string Vekony = ElsoTelepitett("Bahnschrift Light", Alap);
    private static readonly string Kover = ElsoTelepitett("Bahnschrift SemiBold", "Segoe UI Semibold", Alap);

    public static readonly CultureInfo Magyar = CultureInfo.GetCultureInfo("hu-HU");

    public static bool VorosMod { get; set; }
    public static Szinek Sz => VorosMod ? Voros : Normal;

    public static Font Betu(float pont) => Keres(pont, Alap);
    public static Font VekonyBetu(float pont) => Keres(pont, Vekony);
    public static Font KoverBetu(float pont) => Keres(pont, Kover);

    public static int Skala(this Control c, int ertek) => (int)Math.Round(ertek * c.DeviceDpi / 96.0);

    public static void Ir(Graphics g, string szoveg, Font betu, Color szin, Rectangle hely,
        TextFormatFlags jelzok = TextFormatFlags.Left)
    {
        TextRenderer.DrawText(g, szoveg, betu, hely, szin,
            jelzok | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    public static Size Meret(string szoveg, Font betu) =>
        TextRenderer.MeasureText(szoveg, betu, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

    public static void KepRajzol(Graphics g, Image kep, Rectangle hely)
    {
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        if (!VorosMod)
        {
            g.DrawImage(kep, hely);
            return;
        }

        using var jellemzok = new ImageAttributes();
        jellemzok.SetColorMatrix(VorosMatrix);
        g.DrawImage(kep, hely, 0, 0, kep.Width, kep.Height, GraphicsUnit.Pixel, jellemzok);
    }

    public static string Ido(double unix) =>
        DateTimeOffset.FromUnixTimeSeconds((long)unix).ToLocalTime().ToString("HH:mm", Magyar);

    public static string Datum(double unix) =>
        DateTimeOffset.FromUnixTimeSeconds((long)unix).ToLocalTime().ToString("MMM d. HH:mm", Magyar);

    public static string Bajt(double bajt)
    {
        const double gb = 1024d * 1024 * 1024;
        return bajt >= gb
            ? (bajt / gb).ToString("0.0", Magyar) + " GB"
            : (bajt / (1024 * 1024)).ToString("0", Magyar) + " MB";
    }

    public static void Cimsor(Form ablak)
    {
        if (!ablak.IsHandleCreated) return;
        var sotet = 1;
        _ = DwmSetWindowAttribute(ablak.Handle, 20, ref sotet, sizeof(int));
        var szin = ColorTranslator.ToWin32(Sz.Eg);
        _ = DwmSetWindowAttribute(ablak.Handle, 35, ref szin, sizeof(int));
    }

    public static void SotetGorgeto(Control c)
    {
        if (c.IsHandleCreated) _ = SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
    }

    private static Font Keres(float pont, string csalad)
    {
        if (Betuk.TryGetValue((pont, csalad), out var betu)) return betu;
        betu = new Font(csalad, pont, FontStyle.Regular, GraphicsUnit.Point);
        Betuk[(pont, csalad)] = betu;
        return betu;
    }

    private static string ElsoTelepitett(params string[] csaladok)
    {
        using var telepitett = new InstalledFontCollection();
        foreach (var csalad in csaladok)
        {
            if (telepitett.Families.Any(f => f.Name.Equals(csalad, StringComparison.OrdinalIgnoreCase))) return csalad;
        }

        return SystemFonts.DefaultFont.FontFamily.Name;
    }

    private static Color Hex(string s) => ColorTranslator.FromHtml(s);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr ablak, int jellemzo, ref int ertek, int meret);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr ablak, string? alkalmazas, string? azonositok);
}

public sealed class LaposGomb : Button
{
    private bool _folotte;

    public bool Kiemelt { get; set; }

    public LaposGomb()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _folotte = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _folotte = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var sz = Tema.Sz;
        var hatter = Kiemelt && Enabled ? sz.Oiii : _folotte && Enabled ? sz.Vonal : sz.Eg2;
        var szoveg = !Enabled ? sz.Halk : Kiemelt ? sz.Eg : sz.Feny;
        e.Graphics.Clear(hatter);
        using var keret = new Pen(Focused ? sz.Feny : Kiemelt && Enabled ? sz.Oiii : sz.Vonal, Focused ? this.Skala(2) : 1);
        keret.Alignment = PenAlignment.Inset;
        e.Graphics.DrawRectangle(keret, 0, 0, Width - 1, Height - 1);
        Tema.Ir(e.Graphics, Text, Font, szoveg, ClientRectangle,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
