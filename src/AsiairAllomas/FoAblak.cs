using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace AsiairAllomas;

public sealed class FoAblak : Form
{
    private const int LassuFrissites = 30_000;
    private const int GyorsFrissites = 5_000;
    private const int FrissitesKereses = 6 * 60 * 60 * 1000;
    private const string PanelCim = "https://asiair.park.lan/";

    private readonly Beallitas _beallitas = Beallitas.Betolt();
    private readonly System.Windows.Forms.Timer _idozito = new();
    private readonly System.Windows.Forms.Timer _frissitesIdozito = new();
    private readonly Frissito _frissito = new();
    private readonly Panel _fej = new();
    private readonly Panel _lab = new();
    private readonly EgSav _eg = new();
    private readonly ListBox _lista = new();
    private readonly Galeria _galeria = new();
    private readonly LaposGomb _frissites = new() { Kiemelt = true, Visible = false };
    private readonly LaposGomb _kereses = new() { Dock = DockStyle.Right };
    private readonly LaposGomb _mentes = new() { Text = "Mentés most", Kiemelt = true };
    private readonly LaposGomb _fotok = new() { Text = "Fotók mappája" };
    private readonly LaposGomb _eszkoz = new() { Text = "ASIAIR megosztása" };
    private readonly LaposGomb _panel = new() { Text = "Webes panel" };
    private readonly LaposGomb _vorosGomb = new();
    private readonly Label _allapot = new();
    private readonly Label _labSzoveg = new();

    private Kapcsolat? _kapcsolat;
    private PanelAdat? _adat;
    private string? _kivalasztott;
    private string? _uzenet;
    private string? _frissitoSzoveg;
    private UjVerzio? _uj;
    private bool _frissul;

    public FoAblak()
    {
        Text = "ASIAIR Állomás";
        Icon = Ikon();
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Tema.VorosMod = _beallitas.VorosMod;

        _allapot.AutoEllipsis = true;
        _allapot.AccessibleName = "ASIAIR állapota";
        _labSzoveg.AutoEllipsis = true;
        _labSzoveg.Dock = DockStyle.Fill;
        _labSzoveg.TextAlign = ContentAlignment.MiddleLeft;
        _lab.Controls.Add(_labSzoveg);
        _kereses.Text = $"Verzió {Frissito.Jelenlegi}: frissítés keresése";
        _lab.Controls.Add(_kereses);

        _lista.DrawMode = DrawMode.OwnerDrawFixed;
        _lista.BorderStyle = BorderStyle.None;
        _lista.IntegralHeight = false;
        _lista.Dock = DockStyle.Left;
        _lista.AccessibleName = "Célpontok";
        _lista.DrawItem += ListaElemRajzol;
        _lista.SelectedIndexChanged += async (_, _) => await ValasztasAsync();

        _galeria.Dock = DockStyle.Fill;
        _galeria.Megnyitas += FajlMegnyit;
        _eg.Dock = DockStyle.Top;
        _fej.Dock = DockStyle.Top;
        _lab.Dock = DockStyle.Bottom;

        _fej.Controls.AddRange([_allapot, _frissites, _mentes, _fotok, _eszkoz, _panel, _vorosGomb]);
        _fej.Resize += (_, _) => FejElrendez();

        Controls.Add(_galeria);
        Controls.Add(_lista);
        Controls.Add(_lab);
        Controls.Add(_eg);
        Controls.Add(_fej);

        _mentes.Click += async (_, _) => await MentesAsync();
        _fotok.Click += (_, _) => MegosztasMegnyit(null);
        _eszkoz.Click += (_, _) => Megnyit($@"\\{_adat?.Eszkoz.Cim ?? "asiair.local"}");
        _panel.Click += (_, _) => Megnyit(PanelCim);
        _vorosGomb.Click += (_, _) => VorosValt();
        _frissites.Click += async (_, _) => await FrissitesTelepitAsync();
        _kereses.Click += async (_, _) => await FrissitesKeresAsync(true);

        _idozito.Interval = LassuFrissites;
        _idozito.Tick += async (_, _) => await FrissitAsync();
        _frissitesIdozito.Interval = FrissitesKereses;
        _frissitesIdozito.Tick += async (_, _) => await FrissitesKeresAsync(false);

        MeretekAlkalmaz();
        TemaAlkalmaz();
    }

    public static Icon Ikon()
    {
        using var folyam = Assembly.GetExecutingAssembly().GetManifestResourceStream("ikon.ico");
        return folyam is null ? SystemIcons.Application : new Icon(folyam);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Tema.Cimsor(this);
        Tema.SotetGorgeto(_lista);
        _ = Frissito.RegiTorolAsync();
        _ = FrissitesKeresAsync(false);
        _frissitesIdozito.Start();
        var jelszo = _beallitas.Jelszo();
        if (jelszo is not null)
        {
            _kapcsolat = new Kapcsolat(_beallitas.Felhasznalo, jelszo);
            _galeria.Kapcsolatot(_kapcsolat);
            await FrissitAsync();
        }
        else if (!await BelepAsync(null))
        {
            Close();
            return;
        }

        if (!IsDisposed) _idozito.Start();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        MeretekAlkalmaz();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            _ = FrissitAsync();
            e.Handled = true;
        }
        else if (e is { Control: true, KeyCode: Keys.R })
        {
            VorosValt();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _idozito.Dispose();
        _frissitesIdozito.Dispose();
        _frissito.Dispose();
        _kapcsolat?.Dispose();
        base.OnFormClosed(e);
    }

    private void MeretekAlkalmaz()
    {
        MinimumSize = new Size(this.Skala(900), this.Skala(600));
        if (WindowState == FormWindowState.Normal && Width < this.Skala(1180)) Size = new Size(this.Skala(1180), this.Skala(780));
        _fej.Height = this.Skala(60);
        _eg.Height = this.Skala(172);
        _lab.Height = this.Skala(30);
        _lab.Padding = new Padding(this.Skala(20), 0, this.Skala(20), 0);
        _lista.Width = this.Skala(310);
        _lista.ItemHeight = Math.Min(255, this.Skala(62));
        _lista.Font = Tema.Betu(10);
        _allapot.Font = Tema.Betu(10);
        _labSzoveg.Font = Tema.Betu(9);
        foreach (var gomb in Gombok()) gomb.Font = Tema.Betu(10);
        _kereses.Font = Tema.Betu(9);
        _kereses.Width = Tema.Meret(_kereses.Text, _kereses.Font).Width + this.Skala(20);
        FejElrendez();
    }

    private LaposGomb[] Gombok() => [_frissites, _mentes, _fotok, _eszkoz, _panel, _vorosGomb];

    private void FejElrendez()
    {
        var magas = this.Skala(32);
        var y = (_fej.Height - magas) / 2;
        var x = _fej.Width - this.Skala(20);
        foreach (var gomb in Gombok().Reverse())
        {
            if (!gomb.Visible) continue;
            var szeles = Tema.Meret(gomb.Text, gomb.Font).Width + this.Skala(26);
            x -= szeles;
            gomb.SetBounds(x, y, szeles, magas);
            x -= this.Skala(8);
        }

        _allapot.SetBounds(this.Skala(20), 0, Math.Max(0, x - this.Skala(20)), _fej.Height);
        _allapot.TextAlign = ContentAlignment.MiddleLeft;
    }

    private void TemaAlkalmaz()
    {
        var sz = Tema.Sz;
        BackColor = sz.Eg;
        ForeColor = sz.Feny;
        _fej.BackColor = sz.Eg2;
        _lab.BackColor = sz.Eg2;
        _lista.BackColor = sz.Eg2;
        _lista.ForeColor = sz.Feny;
        _galeria.BackColor = sz.Eg;
        _allapot.BackColor = sz.Eg2;
        _labSzoveg.BackColor = sz.Eg2;
        _labSzoveg.ForeColor = sz.Halk;
        _vorosGomb.Text = Tema.VorosMod ? "Normál színek" : "Vörös mód";
        Tema.Cimsor(this);
        AllapotKiir();
        FejElrendez();
        foreach (Control c in Controls) c.Invalidate(true);
    }

    private void VorosValt()
    {
        Tema.VorosMod = !Tema.VorosMod;
        _beallitas.VorosMod = Tema.VorosMod;
        BeallitasMent();
        TemaAlkalmaz();
    }

    private void BeallitasMent()
    {
        try
        {
            _beallitas.Ment();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _uzenet = "A beállítások nem menthetők: " + e.Message;
            AllapotKiir();
        }
    }

    private async Task<bool> BelepAsync(string? uzenet)
    {
        _idozito.Stop();
        using var ablak = new BelepoAblak(_beallitas.Felhasznalo, uzenet);
        Kapcsolat? uj = null;
        ablak.Probalkozas = async (nev, jelszo) =>
        {
            var probalo = new Kapcsolat(nev, jelszo);
            try
            {
                await probalo.PanelAsync();
                uj = probalo;
                return null;
            }
            catch (KapcsolatHiba h)
            {
                probalo.Dispose();
                return h.Message;
            }
        };
        if (ablak.ShowDialog(this) != DialogResult.OK || uj is null) return false;

        _beallitas.Felhasznalo = ablak.Felhasznalo;
        _beallitas.JelszoBeallit(ablak.Jelszo);
        BeallitasMent();
        _kapcsolat?.Dispose();
        _kapcsolat = uj;
        _galeria.Kapcsolatot(uj);
        await FrissitAsync();
        _idozito.Start();
        return true;
    }

    private async Task FrissitAsync()
    {
        if (_frissul || _kapcsolat is null) return;
        _frissul = true;
        try
        {
            var adat = await _kapcsolat.PanelAsync();
            _adat = adat;
            _uzenet = null;
            _eg.Beallit(adat.Ejszaka);
            ListaFeltolt(adat);
            _idozito.Interval = adat.Mentes.Fut ? GyorsFrissites : LassuFrissites;
        }
        catch (KapcsolatHiba h) when (h.Belepes)
        {
            _frissul = false;
            if (!await BelepAsync("A mentett jelszó már nem jó. Add meg újra.")) Close();
            return;
        }
        catch (KapcsolatHiba h)
        {
            _uzenet = h.Message;
        }
        finally
        {
            _frissul = false;
            if (!IsDisposed) AllapotKiir();
        }
    }

    private void ListaFeltolt(PanelAdat adat)
    {
        var nevek = adat.Celpontok.Select(c => c.Nev).ToList();
        var regi = _lista.Items.Cast<Celpont>().Select(c => c.Nev).ToList();
        if (nevek.SequenceEqual(regi))
        {
            _lista.BeginUpdate();
            for (var i = 0; i < adat.Celpontok.Count; i++) _lista.Items[i] = adat.Celpontok[i];
            _lista.EndUpdate();
            return;
        }

        _lista.BeginUpdate();
        _lista.Items.Clear();
        foreach (var c in adat.Celpontok) _lista.Items.Add(c);
        _lista.EndUpdate();
        var hely = nevek.IndexOf(_kivalasztott ?? "");
        _lista.SelectedIndex = hely >= 0 ? hely : nevek.Count > 0 ? 0 : -1;
    }

    private async Task ValasztasAsync()
    {
        var celpont = _lista.SelectedItem as Celpont;
        if (celpont?.Nev == _kivalasztott) return;
        _kivalasztott = celpont?.Nev;
        await _galeria.BetoltAsync(celpont);
    }

    private void ListaElemRajzol(object? kuldo, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _lista.Items.Count) return;
        var sz = Tema.Sz;
        var c = (Celpont)_lista.Items[e.Index];
        var kijelolt = e.State.HasFlag(DrawItemState.Selected);
        using (var hatter = new SolidBrush(kijelolt ? sz.Vonal : sz.Eg2)) e.Graphics.FillRectangle(hatter, e.Bounds);
        var margo = this.Skala(20);
        var nevBetu = Tema.KoverBetu(11);
        var nevMagas = Tema.Meret("Ág", nevBetu).Height;
        var ido = c.IdoSzoveg;
        var idoSzeles = Tema.Meret(ido, Tema.Betu(10)).Width;
        var y = e.Bounds.Top + this.Skala(9);
        Tema.Ir(e.Graphics, c.Nev, nevBetu, sz.Feny, new Rectangle(margo, y, e.Bounds.Width - 2 * margo - idoSzeles - 8, nevMagas));
        Tema.Ir(e.Graphics, ido, Tema.Betu(10), sz.Halk,
            new Rectangle(e.Bounds.Width - margo - idoSzeles - 2, y + 2, idoSzeles + 2, nevMagas));

        var legtobb = Math.Max(1, _lista.Items.Cast<Celpont>().Max(x => x.Mp));
        var sav = new Rectangle(margo, y + nevMagas + this.Skala(7), e.Bounds.Width - 2 * margo, this.Skala(5));
        using (var alap = new SolidBrush(kijelolt ? sz.Eg2 : sz.Vonal)) e.Graphics.FillRectangle(alap, sav);
        using (var kitolto = new SolidBrush(sz.Oiii))
            e.Graphics.FillRectangle(kitolto, sav.X, sav.Y, Math.Max(2, (int)(sav.Width * (c.Mp / legtobb))), sav.Height);
        if (e.State.HasFlag(DrawItemState.Focus) && _lista.Focused)
        {
            using var toll = new Pen(sz.Feny, this.Skala(2)) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset };
            e.Graphics.DrawRectangle(toll, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }
    }

    private void AllapotKiir()
    {
        var sz = Tema.Sz;
        if (_uzenet is not null)
        {
            _allapot.Text = _uzenet;
            _allapot.ForeColor = sz.Ha;
        }
        else if (_adat is null)
        {
            _allapot.Text = "Csatlakozás a picloudhoz.";
            _allapot.ForeColor = sz.Halk;
        }
        else
        {
            _allapot.Text = _adat.Eszkoz.Elerheto ? "Az ASIAIR be van kapcsolva" : "Az ASIAIR ki van kapcsolva, a mentett fotók elérhetők";
            _allapot.ForeColor = _adat.Eszkoz.Elerheto ? sz.Oiii : sz.Feny;
        }

        var fut = _adat?.Mentes.Fut == true;
        _mentes.Enabled = _adat is not null && !fut && _adat.Eszkoz.Elerheto;
        _mentes.Text = fut ? "Mentés fut" : "Mentés most";
        _eszkoz.Visible = _adat?.Eszkoz.Elerheto == true;
        _fotok.Enabled = _kapcsolat?.AktivCim is not null;
        _labSzoveg.Text = LabSzoveg();
        FejElrendez();
    }

    private string LabSzoveg()
    {
        var reszek = new List<string>();
        if (_frissitoSzoveg is not null) reszek.Add(_frissitoSzoveg);
        if (_adat is null) return string.Join("; ", reszek);
        var m = _adat.Mentes;
        if (m is { Fut: true, Haladas: { } h })
        {
            var szazalek = h.Arany is { } arany ? $", {arany * 100:0} %" : "";
            reszek.Add($"Mentés fut: {h.Fajlok:0} fájl, {Tema.Bajt(h.Bajt)}{szazalek}");
        }
        else if (!string.IsNullOrEmpty(m.Utolso))
        {
            reszek.Add($"Utolsó mentés: {m.Utolso}" + (m.UtolsoEredmeny == "ok" ? "" : $" ({m.UtolsoEredmeny})"));
        }

        if (m.Kovetkezo is { } kov && !m.Fut) reszek.Add($"következő ellenőrzés {Tema.Ido(kov)}");
        var l = _adat.Lemez;
        reszek.Add(l.Bedugva
            ? $"SSD: {Tema.Bajt(l.Szabad)} szabad" + (l.EjszakaEleg is { } ej ? $", kb. {ej:0} éjszakára elég" : "")
            : "Az SSD nincs csatlakoztatva");
        return string.Join("; ", reszek);
    }

    private async Task FrissitesKeresAsync(bool kezi)
    {
        _kereses.Enabled = false;
        if (kezi) _frissitoSzoveg = null;
        try
        {
            _uj = await _frissito.KeresAsync();
            _frissites.Text = _uj is null ? "" : $"Frissítés erre: {_uj.Verzio}";
            _frissites.Visible = _uj is not null;
            if (kezi && _uj is null) _frissitoSzoveg = $"A legfrissebb verzió fut ({Frissito.Jelenlegi})";
        }
        catch (FrissitesHiba h)
        {
            if (kezi) _frissitoSzoveg = h.Message;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (IsDisposed) return;
        _kereses.Enabled = true;
        AllapotKiir();
    }

    private async Task FrissitesTelepitAsync()
    {
        if (_uj is not { } uj) return;
        _frissites.Enabled = false;
        _kereses.Enabled = false;
        var haladas = new Progress<int>(szazalek =>
        {
            _frissitoSzoveg = $"Frissítés letöltése: {szazalek} %";
            _labSzoveg.Text = LabSzoveg();
        });
        try
        {
            await _frissito.TelepitAsync(uj, haladas);
            Close();
        }
        catch (FrissitesHiba h)
        {
            _frissitoSzoveg = h.Message;
            _frissites.Enabled = true;
            _kereses.Enabled = true;
            AllapotKiir();
        }
    }

    private async Task MentesAsync()
    {
        if (_kapcsolat is null) return;
        _mentes.Enabled = false;
        try
        {
            await _kapcsolat.MentesAsync();
            _uzenet = null;
            _idozito.Interval = GyorsFrissites;
            await FrissitAsync();
        }
        catch (KapcsolatHiba h)
        {
            _uzenet = h.Message;
            AllapotKiir();
        }
    }

    private void FajlMegnyit(Felvetel f) => MegosztasMegnyit(f.Ut);

    private void MegosztasMegnyit(string? relativ)
    {
        var cim = _kapcsolat?.AktivCim;
        if (cim is null) return;
        var gyoker = $@"\\{cim}\asiair-fotok";
        if (relativ is null)
        {
            Megnyit(gyoker);
            return;
        }

        var teljes = gyoker + @"\" + relativ.Replace('/', '\\');
        try
        {
            Process.Start("explorer.exe", $"/select,\"{teljes}\"");
        }
        catch (Win32Exception e)
        {
            _uzenet = "A Fájlkezelő nem nyitható meg: " + e.Message;
            AllapotKiir();
        }
    }

    private void Megnyit(string cel)
    {
        try
        {
            Process.Start(new ProcessStartInfo(cel) { UseShellExecute = true });
        }
        catch (Win32Exception e)
        {
            _uzenet = $"{cel} nem nyitható meg: {e.Message}";
            AllapotKiir();
        }
    }
}
