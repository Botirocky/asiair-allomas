using System.Drawing.Drawing2D;

namespace AsiairAllomas;

public sealed class EgSav : Control
{
    private Ejszaka? _adat;
    private int? _egerX;

    public EgSav()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        AccessibleRole = AccessibleRole.Graphic;
    }

    public void Beallit(Ejszaka? adat)
    {
        _adat = adat;
        AccessibleName = Cim(adat) + ". " + Reszlet(adat);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _egerX = e.X;
        Invalidate();
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _egerX = null;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var sz = Tema.Sz;
        g.Clear(sz.Eg);
        var margo = this.Skala(20);
        var cimBetu = Tema.VekonyBetu(21);
        var cimMagas = Tema.Meret("Ág", cimBetu).Height;
        Tema.Ir(g, Cim(_adat), cimBetu, sz.Feny, new Rectangle(margo, this.Skala(6), Width - 2 * margo, cimMagas));
        var reszletY = this.Skala(6) + cimMagas + this.Skala(2);
        var reszletMagas = Tema.Meret("Ág", Tema.Betu(10)).Height;
        const string jelmagyarazat = "Fent a felhőzet, a vonal a hold magassága";
        var jelSzeles = Tema.Meret(jelmagyarazat, Tema.Betu(9)).Width;
        var jelLathato = _adat is { Elerheto: true } && Width - 2 * margo > jelSzeles + this.Skala(560);
        if (jelLathato)
            Tema.Ir(g, jelmagyarazat, Tema.Betu(9), sz.Halk, new Rectangle(Width - margo - jelSzeles - 2, reszletY, jelSzeles + 2, reszletMagas));
        Tema.Ir(g, Reszlet(_adat), Tema.Betu(10), sz.Halk,
            new Rectangle(margo, reszletY, Width - 2 * margo - (jelLathato ? jelSzeles + this.Skala(16) : 0), reszletMagas));

        var felul = reszletY + reszletMagas + this.Skala(10);
        var feliratMagas = Tema.Meret("00", Tema.Betu(9)).Height + this.Skala(6);
        var sav = new Rectangle(margo, felul, Width - 2 * margo, Math.Max(this.Skala(30), Height - felul - feliratMagas));
        if (_adat is not { Elerheto: true, Tartomany.Count: 2 })
        {
            using var ures = new SolidBrush(sz.Eg2);
            g.FillRectangle(ures, sav);
            return;
        }

        var t0 = _adat.Tartomany[0];
        var t1 = _adat.Tartomany[1];
        double X(double t) => sav.Left + (t - t0) / (t1 - t0) * sav.Width;

        EgRajzol(g, sav, X);
        FelhoRajzol(g, sav, X);
        HoldRajzol(g, sav, X);
        Orak(g, sav, t0, t1, X, felul + sav.Height + this.Skala(4));

        var most = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (most >= t0 && most <= t1)
        {
            using var toll = new Pen(sz.Oiii, this.Skala(2));
            var x = (float)X(most);
            g.DrawLine(toll, x, sav.Top, x, sav.Bottom);
        }

        if (_egerX is { } egerX && egerX >= sav.Left && egerX <= sav.Right)
            Leolvasas(g, sav, egerX, t0 + (egerX - sav.Left) / (double)sav.Width * (t1 - t0));
    }

    private void EgRajzol(Graphics g, Rectangle sav, Func<double, double> x)
    {
        var mintak = _adat!.Mintak;
        for (var i = 0; i < mintak.Count; i++)
        {
            var bal = (float)x(mintak[i].T);
            var jobb = i + 1 < mintak.Count ? (float)x(mintak[i + 1].T) : sav.Right;
            if (i == 0) bal = sav.Left;
            using var ecset = new SolidBrush(ZonaSzin(mintak[i].Nap));
            g.FillRectangle(ecset, bal, sav.Top, jobb - bal + 1, sav.Height);
        }
    }

    private void FelhoRajzol(Graphics g, Rectangle sav, Func<double, double> x)
    {
        var sz = Tema.Sz;
        foreach (var ora in _adat!.Orak)
        {
            if (ora.CloudCover is not { } felho || felho <= 0) continue;
            var bal = Math.Max((float)x(ora.T), sav.Left);
            var jobb = Math.Min((float)x(ora.T + 3600), sav.Right);
            if (jobb <= bal) continue;
            var magas = (float)(felho / 100 * sav.Height * 0.62);
            using var ecset = new SolidBrush(Color.FromArgb(Tema.VorosMod ? 150 : 96, sz.Felho));
            g.FillRectangle(ecset, bal + 1, sav.Top, jobb - bal - 2, magas);
        }
    }

    private void HoldRajzol(Graphics g, Rectangle sav, Func<double, double> x)
    {
        var pontok = new List<PointF>();
        using var toll = new Pen(Tema.Sz.Hold, this.Skala(2)) { LineJoin = LineJoin.Round };
        var regi = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var minta in _adat!.Mintak)
        {
            if (minta.Hold > 0)
            {
                var y = sav.Bottom - (float)(Math.Min(minta.Hold, 75) / 75 * (sav.Height - this.Skala(8))) - this.Skala(2);
                pontok.Add(new PointF((float)x(minta.T), y));
                continue;
            }

            if (pontok.Count > 1) g.DrawLines(toll, pontok.ToArray());
            pontok.Clear();
        }

        if (pontok.Count > 1) g.DrawLines(toll, pontok.ToArray());
        g.SmoothingMode = regi;
    }

    private void Orak(Graphics g, Rectangle sav, double t0, double t1, Func<double, double> x, int y)
    {
        var sz = Tema.Sz;
        var betu = Tema.Betu(9);
        var elso = Math.Ceiling(t0 / 3600) * 3600;
        var lepes = sav.Width < this.Skala(760) ? 7200 : 3600;
        for (var t = elso; t <= t1; t += 3600)
        {
            var helyi = DateTimeOffset.FromUnixTimeSeconds((long)t).ToLocalTime();
            if (lepes == 7200 && helyi.Hour % 2 != 0) continue;
            var felirat = helyi.ToString("HH");
            var meret = Tema.Meret(felirat, betu);
            var bal = (int)x(t) - meret.Width / 2;
            if (bal < sav.Left || bal + meret.Width > sav.Right) continue;
            Tema.Ir(g, felirat, betu, sz.Halk, new Rectangle(bal, y, meret.Width + 2, meret.Height));
        }
    }

    private void Leolvasas(Graphics g, Rectangle sav, int x, double t)
    {
        var sz = Tema.Sz;
        using (var toll = new Pen(sz.Feny)) g.DrawLine(toll, x, sav.Top, x, sav.Bottom);
        var ora = _adat!.Orak.FirstOrDefault(o => t >= o.T && t < o.T + 3600);
        var reszek = new List<string> { Tema.Ido(t) };
        if (ora?.CloudCover is { } felho) reszek.Add($"felhő {felho:0} %");
        if (ora?.Temperature2m is { } hofok) reszek.Add($"{hofok.ToString("0.0", Tema.Magyar)} °C");
        if (ora?.DewPoint2m is { } harmat) reszek.Add($"harmatpont {harmat.ToString("0.0", Tema.Magyar)} °C");
        if (ora?.WindSpeed10m is { } szel) reszek.Add($"szél {szel:0} km/h");
        var szoveg = string.Join(", ", reszek);
        var betu = Tema.Betu(10);
        var meret = Tema.Meret(szoveg, betu);
        var parna = this.Skala(6);
        var doboz = new Rectangle(0, sav.Bottom - meret.Height - 2 * parna, meret.Width + 2 * parna, meret.Height + 2 * parna);
        doboz.X = Math.Clamp(x + this.Skala(8), sav.Left, sav.Right - doboz.Width);
        if (x + this.Skala(8) + doboz.Width > sav.Right) doboz.X = Math.Max(sav.Left, x - this.Skala(8) - doboz.Width);
        using (var ecset = new SolidBrush(sz.Eg)) g.FillRectangle(ecset, doboz);
        Tema.Ir(g, szoveg, betu, sz.Feny, new Rectangle(doboz.X + parna, doboz.Y + parna, meret.Width + 2, meret.Height));
    }

    private static Color ZonaSzin(double nap)
    {
        var sz = Tema.Sz;
        return nap > 0 ? sz.SavNap : nap > -6 ? sz.SavPolgari : nap > -12 ? sz.SavNavig : nap > -18 ? sz.SavCsill : sz.SavSotet;
    }

    private static string Cim(Ejszaka? ej)
    {
        if (ej is null) return "Ma éjjel";
        if (!ej.Elerheto) return "Ma éjjel: még nincs előrejelzés";
        return ej.Iteles?.Szint switch
        {
            "jo" => "Ma éjjel jó lesz fotózni",
            "reszben" => "Ma éjjel csak részben lesz tiszta",
            "rossz" => "Ma éjjel felhős lesz",
            _ => "Ma éjjel: még nincs előrejelzés",
        };
    }

    private static string Reszlet(Ejszaka? ej)
    {
        if (ej is null) return "Csatlakozás a picloudhoz.";
        if (!ej.Elerheto) return ej.Uzenet ?? "A fotózás helye még nem ismert.";
        var reszek = new List<string>();
        if (ej.Sotet is { Kezdet: { } k, Vege: { } v })
            reszek.Add((ej.Sotet.Csillagaszati ? "Teljes sötétség " : "Legsötétebb ") + $"{Tema.Ido(k)} és {Tema.Ido(v)} között");
        else
            reszek.Add("Ma nem lesz teljesen sötét");
        if (ej.Iteles is { TisztaOrak: var tiszta, AtlagFelho: { } felho })
            reszek.Add($"{tiszta} tiszta óra, átlagosan {felho:0} % felhő");
        if (ej.Hold is { } hold)
        {
            var holdSzoveg = $"{hold.Nev ?? "hold"} {hold.Megvilagitas * 100:0} %";
            if (hold.Kel is { } kel) holdSzoveg += $", kel {Tema.Ido(kel)}";
            reszek.Add(holdSzoveg);
        }

        if (!string.IsNullOrEmpty(ej.IdojarasHiba)) reszek.Add("az időjárás most nem frissíthető");
        return string.Join("; ", reszek);
    }
}
