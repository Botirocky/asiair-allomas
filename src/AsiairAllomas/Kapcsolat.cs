using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace AsiairAllomas;

public sealed class KapcsolatHiba(string uzenet, HttpStatusCode? kod = null) : Exception(uzenet)
{
    public HttpStatusCode? Kod { get; } = kod;
    public bool Belepes => Kod == HttpStatusCode.Unauthorized;
}

public sealed class Kapcsolat : IDisposable
{
    private const string Nev = "asiair.park.lan";
    private static readonly string[] TartalekCimek = ["192.168.1.185", "100.84.183.63"];
    private static readonly TimeSpan CsatlakozasIdo = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions JsonBeallitas = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _kliens;
    private readonly X509Certificate2 _gyoker;
    private volatile string? _jo;

    public string? AktivCim { get; private set; }

    public Kapcsolat(string felhasznalo, string jelszo)
    {
        _gyoker = GyokerBetolt();
        var kezelo = new SocketsHttpHandler
        {
            ConnectCallback = KapcsolodjAsync,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = TanusitvanyJo },
        };
        _kliens = new HttpClient(kezelo)
        {
            BaseAddress = new Uri($"https://{Nev}/"),
            Timeout = TimeSpan.FromSeconds(25),
        };
        var azonosito = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{felhasznalo}:{jelszo}"));
        _kliens.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", azonosito);
    }

    public Task<PanelAdat> PanelAsync(CancellationToken megszakit = default) =>
        JsonAsync<PanelAdat>("api/v1/panel", megszakit);

    public Task<List<Felvetel>> CelpontAsync(string nev, CancellationToken megszakit = default) =>
        JsonAsync<List<Felvetel>>("api/v1/celpont?nev=" + Uri.EscapeDataString(nev), megszakit);

    public async Task<byte[]> KepAsync(string ut, CancellationToken megszakit = default)
    {
        using var valasz = await KuldAsync(new HttpRequestMessage(HttpMethod.Get, "kep?ut=" + Uri.EscapeDataString(ut)), megszakit);
        return await valasz.Content.ReadAsByteArrayAsync(megszakit);
    }

    public async Task MentesAsync(CancellationToken megszakit = default)
    {
        var keres = new HttpRequestMessage(HttpMethod.Post, "api/v1/mentes");
        keres.Headers.Add("X-Park-Keres", "1");
        using var _ = await KuldAsync(keres, megszakit);
    }

    public void Dispose()
    {
        _kliens.Dispose();
        _gyoker.Dispose();
    }

    private async Task<T> JsonAsync<T>(string ut, CancellationToken megszakit)
    {
        using var valasz = await KuldAsync(new HttpRequestMessage(HttpMethod.Get, ut), megszakit);
        var szoveg = await valasz.Content.ReadAsStringAsync(megszakit);
        try
        {
            return JsonSerializer.Deserialize<T>(szoveg, JsonBeallitas)
                   ?? throw new KapcsolatHiba("A szerver üres választ adott.");
        }
        catch (JsonException)
        {
            throw new KapcsolatHiba("A szerver válasza nem értelmezhető. Frissítsd az alkalmazást.");
        }
    }

    private async Task<HttpResponseMessage> KuldAsync(HttpRequestMessage keres, CancellationToken megszakit)
    {
        HttpResponseMessage valasz;
        try
        {
            using (keres)
            {
                valasz = await _kliens.SendAsync(keres, megszakit);
            }
        }
        catch (HttpRequestException e) when (e.InnerException is System.Security.Authentication.AuthenticationException)
        {
            throw new KapcsolatHiba("A szerver tanúsítványa nem a parké. A jelszót nem küldtem el.");
        }
        catch (HttpRequestException)
        {
            throw new KapcsolatHiba("A picloud nem érhető el. Ellenőrizd, hogy otthon vagy-e, vagy fut-e a Tailscale.");
        }
        catch (TaskCanceledException) when (!megszakit.IsCancellationRequested)
        {
            throw new KapcsolatHiba("A picloud nem válaszolt időben. Később újra próbálom.");
        }

        if (valasz.IsSuccessStatusCode) return valasz;

        var kod = valasz.StatusCode;
        var uzenet = await HibaUzenetAsync(valasz, megszakit);
        valasz.Dispose();
        throw kod switch
        {
            HttpStatusCode.Unauthorized => new KapcsolatHiba("Hibás felhasználónév vagy jelszó.", kod),
            HttpStatusCode.TooManyRequests => new KapcsolatHiba("Túl sok rossz próba: a belépés 15 percre zárolva.", kod),
            _ => new KapcsolatHiba(uzenet ?? $"A szerver hibát jelzett ({(int)kod}).", kod),
        };
    }

    private static async Task<string?> HibaUzenetAsync(HttpResponseMessage valasz, CancellationToken megszakit)
    {
        try
        {
            using var dok = JsonDocument.Parse(await valasz.Content.ReadAsStringAsync(megszakit));
            return dok.RootElement.GetProperty("hiba").GetProperty("uzenet").GetString();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private async ValueTask<Stream> KapcsolodjAsync(SocketsHttpConnectionContext kornyezet, CancellationToken megszakit)
    {
        var jeloltek = new List<string> { Nev };
        jeloltek.AddRange(TartalekCimek);
        if (_jo is { } jo)
        {
            jeloltek.Remove(jo);
            jeloltek.Insert(0, jo);
        }

        foreach (var jelolt in jeloltek)
        {
            var aljzat = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            using var idozito = CancellationTokenSource.CreateLinkedTokenSource(megszakit);
            idozito.CancelAfter(CsatlakozasIdo);
            try
            {
                await aljzat.ConnectAsync(jelolt, kornyezet.DnsEndPoint.Port, idozito.Token);
                _jo = jelolt;
                AktivCim = (aljzat.RemoteEndPoint as IPEndPoint)?.Address.MapToIPv4().ToString();
                return new NetworkStream(aljzat, true);
            }
            catch (Exception e) when (e is SocketException or OperationCanceledException && !megszakit.IsCancellationRequested)
            {
                aljzat.Dispose();
            }
            catch
            {
                aljzat.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("Egyik cím sem érhető el.");
    }

    private bool TanusitvanyJo(object kuldo, X509Certificate? tanusitvany, X509Chain? lanc, SslPolicyErrors hibak)
    {
        if (tanusitvany is null || lanc is null) return false;
        if (hibak.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)) return false;
        lanc.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        lanc.ChainPolicy.CustomTrustStore.Clear();
        lanc.ChainPolicy.CustomTrustStore.Add(_gyoker);
        lanc.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using var tanusitvany2 = new X509Certificate2(tanusitvany);
        return lanc.Build(tanusitvany2);
    }

    private static X509Certificate2 GyokerBetolt()
    {
        using var folyam = Assembly.GetExecutingAssembly().GetManifestResourceStream("park-ca.crt")
                           ?? throw new InvalidOperationException("Hiányzik a beépített tanúsítvány.");
        using var olvaso = new StreamReader(folyam);
        return X509Certificate2.CreateFromPem(olvaso.ReadToEnd());
    }
}
