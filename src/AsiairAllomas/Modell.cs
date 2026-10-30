namespace AsiairAllomas;

public sealed class PanelAdat
{
    public Eszkoz Eszkoz { get; set; } = new();
    public MentesAllapot Mentes { get; set; } = new();
    public Lemez Lemez { get; set; } = new();
    public List<Celpont> Celpontok { get; set; } = [];
    public Ejszaka? Ejszaka { get; set; }
}

public sealed class Eszkoz
{
    public string Cim { get; set; } = "asiair.local";
    public bool Elerheto { get; set; }
}

public sealed class MentesAllapot
{
    public bool Fut { get; set; }
    public Haladas? Haladas { get; set; }
    public double? Kovetkezo { get; set; }
    public string? Utolso { get; set; }
    public string? UtolsoEredmeny { get; set; }
}

public sealed class Haladas
{
    public double Fajlok { get; set; }
    public double Bajt { get; set; }
    public double? Arany { get; set; }
}

public sealed class Lemez
{
    public bool Bedugva { get; set; }
    public double Szabad { get; set; }
    public double Teljes { get; set; }
    public double? EjszakaEleg { get; set; }
}

public sealed class Celpont
{
    public string Nev { get; set; } = "";
    public int Kepek { get; set; }
    public double Mp { get; set; }
    public List<CelpontEjszaka> Ejszakak { get; set; } = [];
    public List<string> Kamerak { get; set; } = [];
    public List<string> Gainek { get; set; } = [];
    public List<double?> Homerseklet { get; set; } = [];
    public double Meret { get; set; }
    public string IdoSzoveg { get; set; } = "";
}

public sealed class CelpontEjszaka
{
    public string Ejszaka { get; set; } = "";
    public int Kepek { get; set; }
    public double Mp { get; set; }
}

public sealed class Felvetel
{
    public string Ut { get; set; } = "";
    public string? Thn { get; set; }
    public double Ido { get; set; }
    public double? Exp { get; set; }
    public double? Homerseklet { get; set; }
    public double Meret { get; set; }
}

public sealed class Ejszaka
{
    public bool Elerheto { get; set; }
    public string? Uzenet { get; set; }
    public EjszakaHold? Hold { get; set; }
    public SotetAblak? Sotet { get; set; }
    public List<double>? Tartomany { get; set; }
    public List<EgMinta> Mintak { get; set; } = [];
    public List<IdojarasOra> Orak { get; set; } = [];
    public Iteles? Iteles { get; set; }
    public string? IdojarasHiba { get; set; }
}

public sealed class EjszakaHold
{
    public double Megvilagitas { get; set; }
    public string? Nev { get; set; }
    public double? Kel { get; set; }
    public double? Nyugszik { get; set; }
}

public sealed class SotetAblak
{
    public double? Kezdet { get; set; }
    public double? Vege { get; set; }
    public bool Csillagaszati { get; set; }
}

public sealed class EgMinta
{
    public double T { get; set; }
    public double Nap { get; set; }
    public double Hold { get; set; }
}

public sealed class IdojarasOra
{
    public double T { get; set; }
    public double? CloudCover { get; set; }
    public double? RelativeHumidity2m { get; set; }
    public double? DewPoint2m { get; set; }
    public double? Temperature2m { get; set; }
    public double? WindSpeed10m { get; set; }
}

public sealed class Iteles
{
    public string Szint { get; set; } = "ismeretlen";
    public int TisztaOrak { get; set; }
    public double? AtlagFelho { get; set; }
}
