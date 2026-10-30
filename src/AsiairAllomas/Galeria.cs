namespace AsiairAllomas;

public sealed class Galeria : Control
{
    private const int EgyszerreLetoltes = 4;

    private readonly Dictionary<string, Image> _kepek = [];
    private readonly HashSet<string> _folyamatban = [];
    private readonly HashSet<string> _hibas = [];
    private readonly SemaphoreSlim _sor = new(EgyszerreLetoltes);

    private Kapcsolat? _kapcsolat;
    private Celpont? _celpont;
    private List<Felvetel> _felvetelek = [];
    private string? _uzenet;
    private int _eltolas;
    private int _kijelolt = -1;
    private int _folotte = -1;
    private int? _huzas;
    private int _huzasEltolas;
    private long _generacio;

    public event Action<Felvetel>? Megnyitas;

    public Galeria()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "A célpont fényképei";
    }

    public void Kapcsolatot(Kapcsolat? kapcsolat) => _kapcsolat = kapcsolat;

    public async Task BetoltAsync(Celpont? celpont)
    {
        var generacio = Interlocked.Increment(ref _generacio);
        _celpont = celpont;
        _felvetelek = [];
        _eltolas = 0;
        _kijelolt = -1;
        _folotte = -1;
        _uzenet = celpont is null ? "Válassz célpontot a listából." : "Felvételek betöltése.";
        Invalidate();
        if (celpont is null || _kapcsolat is null) return;
        try
        {
            var lista = await _kapcsolat.CelpontAsync(celpont.Nev);
            if (generacio != Interlocked.Read(ref _generacio)) return;
            _felvetelek = lista;
            _uzenet = lista.Count == 0 ? "Ehhez a célponthoz nincs felvétel a lemezen." : null;
        }
        catch (KapcsolatHiba h)
        {
            if (generacio == Interlocked.Read(ref _generacio)) _uzenet = h.Message;
        }

        Invalidate();
    }

    protected override bool IsInputKey(Keys billentyu) =>
        billentyu is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End
        || base.IsInputKey(billentyu);

    protected override void OnGotFocus(EventArgs e)
    {
        if (_kijelolt < 0 && _felvetelek.Count > 0) _kijelolt = 0;
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_felvetelek.Count == 0)
        {
            base.OnKeyDown(e);
            return;
        }

        var (oszlop, sorMagas, _) = Racs();
        var egyLap = Math.Max(1, RacsMagas() / sorMagas) * oszlop;
        int? uj = e.KeyCode switch
        {
            Keys.Left => _kijelolt - 1,
            Keys.Right => _kijelolt + 1,
            Keys.Up => _kijelolt - oszlop,
            Keys.Down => _kijelolt + oszlop,
            Keys.PageUp => _kijelolt - egyLap,
            Keys.PageDown => _kijelolt + egyLap,
            Keys.Home => 0,
            Keys.End => _felvetelek.Count - 1,
            _ => null,
        };
        if (uj is { } hova)
        {
            _kijelolt = Math.Clamp(hova, 0, _felvetelek.Count - 1);
            LathatovaTesz(_kijelolt);
            Invalidate();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && _kijelolt >= 0)
        {
            Megnyitas?.Invoke(_felvetelek[_kijelolt]);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Gorget(_eltolas - e.Delta * this.Skala(120) / 120);
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        var gorgeto = GorgetoHelye();
        if (gorgeto is { } hely && e.X >= Width - this.Skala(14))
        {
            if (!hely.Contains(e.Location)) Gorget(_eltolas + (e.Y < hely.Top ? -RacsMagas() : RacsMagas()));
            _huzas = e.Y;
            _huzasEltolas = _eltolas;
            return;
        }

        var talalat = Talalat(e.Location);
        if (talalat >= 0)
        {
            _kijelolt = talalat;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _huzas = null;
        base.OnMouseUp(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        var talalat = Talalat(e.Location);
        if (talalat >= 0) Megnyitas?.Invoke(_felvetelek[talalat]);
        base.OnMouseDoubleClick(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_huzas is { } kezdet)
        {
            var arany = TartalomMagas() / (double)Math.Max(1, RacsMagas());
            Gorget(_huzasEltolas + (int)((e.Y - kezdet) * arany));
            return;
        }

        var talalat = Talalat(e.Location);
        if (talalat != _folotte)
        {
            _folotte = talalat;
            Cursor = talalat >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _folotte = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnResize(EventArgs e)
    {
        Gorget(_eltolas);
        base.OnResize(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var sz = Tema.Sz;
        g.Clear(sz.Eg);
        var margo = this.Skala(20);
        var fej = FejMagas();

        if (_celpont is { } c)
        {
            var nevBetu = Tema.VekonyBetu(21);
            var nevMeret = Tema.Meret(c.Nev, nevBetu);
            Tema.Ir(g, c.Nev, nevBetu, sz.Feny, new Rectangle(margo, this.Skala(12), Width - 2 * margo, nevMeret.Height));
            var betu = Tema.Betu(10);
            var y = this.Skala(12) + nevMeret.Height + this.Skala(2);
            var sorMagas = Tema.Meret("Ág", betu).Height;
            Tema.Ir(g, Osszegzes(c), betu, sz.Halk, new Rectangle(margo, y, Width - 2 * margo, sorMagas));
            y += sorMagas + this.Skala(8);
            EjszakakRajzol(g, c, new Rectangle(margo, y, Width - 2 * margo, this.Skala(8)));
        }

        var racs = new Rectangle(0, fej, Width, Height - fej);
        if (_uzenet is { } uzenet)
        {
            Tema.Ir(g, uzenet, Tema.Betu(11), sz.Halk, new Rectangle(margo, fej + this.Skala(8), Width - 2 * margo, this.Skala(60)),
                TextFormatFlags.WordBreak);
            return;
        }

        var regiTartomany = g.Clip;
        g.SetClip(racs);
        var (oszlop, sor, kepMeret) = Racs();
        var elso = Math.Max(0, _eltolas / sor) * oszlop;
        var utolso = Math.Min(_felvetelek.Count, ((_eltolas + racs.Height) / sor + 1) * oszlop);
        for (var i = elso; i < utolso; i++) BelyegRajzol(g, i, oszlop, sor, kepMeret, fej);
        g.Clip = regiTartomany;

        if (GorgetoHelye() is { } gorgeto)
        {
            using var ecset = new SolidBrush(sz.Vonal);
            g.FillRectangle(ecset, gorgeto);
        }
    }

    private void BelyegRajzol(Graphics g, int i, int oszlop, int sor, Size kepMeret, int fej)
    {
        var sz = Tema.Sz;
        var f = _felvetelek[i];
        var hely = BelyegHely(i, oszlop, sor, kepMeret, fej);
        if (f.Thn is { } thn && _kepek.TryGetValue(thn, out var kep))
        {
            Tema.KepRajzol(g, kep, hely);
        }
        else
        {
            using var ures = new SolidBrush(sz.Eg2);
            g.FillRectangle(ures, hely);
            if (f.Thn is { } ut && !_hibas.Contains(ut)) KepKer(ut);
            else
                Tema.Ir(g, "Nincs előnézet", Tema.Betu(9), sz.Halk, hely,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        if (i == _kijelolt || i == _folotte)
        {
            var kiemelt = i == _kijelolt && Focused;
            using var toll = new Pen(kiemelt ? sz.Feny : i == _kijelolt ? sz.Oiii : sz.Halk, this.Skala(kiemelt ? 3 : 2))
            {
                Alignment = System.Drawing.Drawing2D.PenAlignment.Inset,
            };
            g.DrawRectangle(toll, hely);
        }

        var felirat = Tema.Datum(f.Ido);
        if (f.Exp is { } exp) felirat += $", {exp.ToString("0.#", Tema.Magyar)} s";
        var betu = Tema.Betu(9);
        Tema.Ir(g, felirat, betu, sz.Halk,
            new Rectangle(hely.Left, hely.Bottom + this.Skala(4), hely.Width, Tema.Meret("Ág", betu).Height));
    }

    private void EjszakakRajzol(Graphics g, Celpont c, Rectangle sav)
    {
        var sz = Tema.Sz;
        var osszes = c.Ejszakak.Sum(e => e.Mp);
        if (osszes <= 0) return;
        double x = sav.Left;
        var betu = Tema.Betu(9);
        var cimkeMagas = Tema.Meret("Ág", betu).Height;
        for (var i = 0; i < c.Ejszakak.Count; i++)
        {
            var e = c.Ejszakak[i];
            var szeles = e.Mp / osszes * sav.Width;
            using (var ecset = new SolidBrush(i % 2 == 0 ? sz.Oiii : sz.Hold))
                g.FillRectangle(ecset, (float)x, sav.Top, (float)Math.Max(1, szeles - 2), sav.Height);
            var cimke = e.Ejszaka.Length >= 10 ? e.Ejszaka[5..].Replace('-', '.') + "." : e.Ejszaka;
            if (szeles > Tema.Meret(cimke, betu).Width + this.Skala(6))
                Tema.Ir(g, cimke, betu, sz.Halk,
                    new Rectangle((int)x, sav.Bottom + this.Skala(2), (int)szeles, cimkeMagas));
            x += szeles;
        }
    }

    private static string Osszegzes(Celpont c)
    {
        var reszek = new List<string> { c.IdoSzoveg, $"{c.Kepek} kép", Tema.Bajt(c.Meret) };
        if (c.Kamerak.Count > 0) reszek.Add(string.Join(", ", c.Kamerak));
        if (c.Gainek.Count > 0) reszek.Add("gain " + string.Join(", ", c.Gainek));
        if (c.Homerseklet is [{ } a, { } b])
            reszek.Add(a == b ? $"{a.ToString("0.#", Tema.Magyar)} °C" : $"{a.ToString("0.#", Tema.Magyar)} és {b.ToString("0.#", Tema.Magyar)} °C között");
        return string.Join("; ", reszek);
    }

    private async void KepKer(string ut)
    {
        if (_kapcsolat is null || !_folyamatban.Add(ut)) return;
        var generacio = Interlocked.Read(ref _generacio);
        await _sor.WaitAsync();
        try
        {
            if (generacio != Interlocked.Read(ref _generacio)) return;
            var bajtok = await _kapcsolat.KepAsync(ut);
            using var folyam = new MemoryStream(bajtok);
            using var eredeti = Image.FromStream(folyam);
            var kep = new Bitmap(eredeti);
            _kepek[ut] = kep;
            Invalidate();
        }
        catch (Exception e) when (e is KapcsolatHiba or ArgumentException or ObjectDisposedException or OperationCanceledException or System.Runtime.InteropServices.ExternalException)
        {
            _hibas.Add(ut);
            Invalidate();
        }
        finally
        {
            _sor.Release();
            _folyamatban.Remove(ut);
        }
    }

    private int FejMagas()
    {
        if (_celpont is null) return this.Skala(8);
        var szoveg = Tema.Meret("Ág", Tema.VekonyBetu(21)).Height + Tema.Meret("Ág", Tema.Betu(10)).Height;
        return szoveg + Tema.Meret("Ág", Tema.Betu(9)).Height + this.Skala(46);
    }

    private int RacsMagas() => Math.Max(1, Height - FejMagas());

    private (int oszlop, int sor, Size kepMeret) Racs()
    {
        var hezag = this.Skala(14);
        var hasznos = Math.Max(this.Skala(120), Width - 2 * this.Skala(20) - this.Skala(10));
        var oszlop = Math.Max(1, (hasznos + hezag) / (this.Skala(190) + hezag));
        var szeles = (hasznos - hezag * (oszlop - 1)) / oszlop;
        var magas = szeles * 2 / 3;
        var felirat = Tema.Meret("Ág", Tema.Betu(9)).Height + this.Skala(4);
        return (oszlop, magas + felirat + hezag, new Size(szeles, magas));
    }

    private Rectangle BelyegHely(int i, int oszlop, int sor, Size kepMeret, int fej)
    {
        var hezag = this.Skala(14);
        var x = this.Skala(20) + i % oszlop * (kepMeret.Width + hezag);
        var y = fej + i / oszlop * sor - _eltolas;
        return new Rectangle(x, y, kepMeret.Width, kepMeret.Height);
    }

    private int Talalat(Point p)
    {
        if (p.Y < FejMagas() || _felvetelek.Count == 0) return -1;
        var (oszlop, sor, kepMeret) = Racs();
        var fej = FejMagas();
        var becsult = Math.Max(0, (p.Y - fej + _eltolas) / sor) * oszlop;
        for (var i = becsult; i < Math.Min(_felvetelek.Count, becsult + oszlop); i++)
            if (BelyegHely(i, oszlop, sor, kepMeret, fej).Contains(p)) return i;
        return -1;
    }

    private int TartalomMagas()
    {
        var (oszlop, sor, _) = Racs();
        return (_felvetelek.Count + oszlop - 1) / oszlop * sor;
    }

    private void Gorget(int uj)
    {
        var legtobb = Math.Max(0, TartalomMagas() - RacsMagas());
        var korlatozott = Math.Clamp(uj, 0, legtobb);
        if (korlatozott == _eltolas) return;
        _eltolas = korlatozott;
        Invalidate();
    }

    private void LathatovaTesz(int i)
    {
        var (oszlop, sor, _) = Racs();
        var felul = i / oszlop * sor;
        if (felul < _eltolas) Gorget(felul);
        else if (felul + sor > _eltolas + RacsMagas()) Gorget(felul + sor - RacsMagas());
    }

    private Rectangle? GorgetoHelye()
    {
        var teljes = TartalomMagas();
        var latszo = RacsMagas();
        if (teljes <= latszo) return null;
        var magas = Math.Max(this.Skala(30), latszo * latszo / teljes);
        var y = FejMagas() + (int)((latszo - magas) * (_eltolas / (double)(teljes - latszo)));
        return new Rectangle(Width - this.Skala(8), y, this.Skala(5), magas);
    }
}
