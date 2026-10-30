namespace AsiairAllomas;

public sealed class BelepoAblak : Form
{
    private readonly TextBox _felhasznalo;
    private readonly TextBox _jelszo;
    private readonly Label _hiba;
    private readonly LaposGomb _belep;

    public Func<string, string, Task<string?>>? Probalkozas { get; set; }
    public string Felhasznalo => _felhasznalo.Text.Trim();
    public string Jelszo => _jelszo.Text;

    public BelepoAblak(string felhasznalo, string? uzenet)
    {
        var sz = Tema.Sz;
        Text = "Belépés a parkba";
        Icon = FoAblak.Ikon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = sz.Eg;
        ForeColor = sz.Feny;
        Font = Tema.Betu(10);
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(this.Skala(380), this.Skala(292));

        var margo = this.Skala(20);
        var szeles = ClientSize.Width - 2 * margo;
        Controls.Add(new Label
        {
            Text = "A park belépése kell (ugyanaz, mint a kezdolap.park.lan oldalon). A jelszót csak ezen a Windows-fiókon lehet visszafejteni.",
            ForeColor = sz.Halk,
            Bounds = new Rectangle(margo, this.Skala(16), szeles, this.Skala(62)),
        });
        Controls.Add(Cimke("Felhasználónév", margo, this.Skala(84), szeles));
        _felhasznalo = Mezo(felhasznalo, margo, this.Skala(106), szeles);
        Controls.Add(_felhasznalo);
        Controls.Add(Cimke("Jelszó", margo, this.Skala(140), szeles));
        _jelszo = Mezo("", margo, this.Skala(162), szeles);
        _jelszo.UseSystemPasswordChar = true;
        Controls.Add(_jelszo);
        _hiba = new Label
        {
            Text = uzenet ?? "",
            ForeColor = sz.Ha,
            Bounds = new Rectangle(margo, this.Skala(196), szeles, this.Skala(40)),
        };
        Controls.Add(_hiba);

        var gombMagas = this.Skala(32);
        var kilep = new LaposGomb
        {
            Text = "Mégse",
            DialogResult = DialogResult.Cancel,
            Bounds = new Rectangle(margo, ClientSize.Height - gombMagas - this.Skala(14), this.Skala(100), gombMagas),
        };
        _belep = new LaposGomb
        {
            Text = "Belépés",
            Kiemelt = true,
            Bounds = new Rectangle(ClientSize.Width - margo - this.Skala(120), kilep.Top, this.Skala(120), gombMagas),
        };
        _belep.Click += async (_, _) => await BelepAsync();
        Controls.Add(kilep);
        Controls.Add(_belep);
        AcceptButton = _belep;
        CancelButton = kilep;
        ActiveControl = string.IsNullOrEmpty(felhasznalo) ? _felhasznalo : _jelszo;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Tema.Cimsor(this);
    }

    private async Task BelepAsync()
    {
        if (Felhasznalo.Length == 0 || Jelszo.Length == 0)
        {
            _hiba.Text = "Add meg a felhasználónevet és a jelszót.";
            return;
        }

        if (Probalkozas is null) return;
        _belep.Enabled = false;
        _hiba.Text = "Belépés folyamatban.";
        var hiba = await Probalkozas(Felhasznalo, Jelszo);
        if (IsDisposed) return;
        _belep.Enabled = true;
        if (hiba is null)
        {
            DialogResult = DialogResult.OK;
            return;
        }

        _hiba.Text = hiba;
        _jelszo.SelectAll();
        _jelszo.Focus();
    }

    private static Label Cimke(string szoveg, int x, int y, int szeles) => new()
    {
        Text = szoveg,
        ForeColor = Tema.Sz.Halk,
        Bounds = new Rectangle(x, y, szeles, Tema.Meret("Ág", Tema.Betu(10)).Height + 2),
    };

    private static TextBox Mezo(string szoveg, int x, int y, int szeles) => new()
    {
        Text = szoveg,
        BackColor = Tema.Sz.Eg2,
        ForeColor = Tema.Sz.Feny,
        BorderStyle = BorderStyle.FixedSingle,
        Bounds = new Rectangle(x, y, szeles, 26),
    };
}
