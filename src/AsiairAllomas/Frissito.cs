using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace AsiairAllomas;

public sealed class FrissitesHiba(string uzenet) : Exception(uzenet);

public sealed record UjVerzio(Version Verzio, Uri Letoltes, long Meret, string? Sha256);

public sealed class Frissito : IDisposable
{
    private const string KiadasCim = "https://api.github.com/repos/Botirocky/asiair-allomas/releases/latest";
    private const string CsomagNev = "AsiairAllomas.exe";
    private static readonly TimeSpan ProbaIdo = TimeSpan.FromSeconds(60);

    private readonly HttpClient _kliens = new() { Timeout = Timeout.InfiniteTimeSpan };

    public Frissito()
    {
        _kliens.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AsiairAllomas", Jelenlegi.ToString()));
    }

    public static Version Jelenlegi => Normal(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    private static string Program => Environment.ProcessPath ?? throw new FrissitesHiba("A program helye nem állapítható meg.");
    private static string Regi => Program + ".regi";

    public async Task<UjVerzio?> KeresAsync(CancellationToken megszakit = default)
    {
        using var idozito = CancellationTokenSource.CreateLinkedTokenSource(megszakit);
        idozito.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var keres = new HttpRequestMessage(HttpMethod.Get, KiadasCim);
            keres.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var valasz = await _kliens.SendAsync(keres, idozito.Token);
            if (valasz.StatusCode == HttpStatusCode.NotFound) return null;
            if (!valasz.IsSuccessStatusCode)
                throw new FrissitesHiba($"A GitHub most nem válaszol rendesen ({(int)valasz.StatusCode}). Később próbáld újra.");
            using var dok = JsonDocument.Parse(await valasz.Content.ReadAsStringAsync(idozito.Token));
            return Ertelmez(dok.RootElement);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException && !megszakit.IsCancellationRequested)
        {
            throw new FrissitesHiba("A GitHub nem érhető el. Ellenőrizd az internetkapcsolatot.");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new FrissitesHiba("A GitHub válasza nem értelmezhető.");
        }
    }

    public async Task TelepitAsync(UjVerzio uj, IProgress<int> haladas, CancellationToken megszakit = default)
    {
        var mappa = Path.GetDirectoryName(Program) ?? throw new FrissitesHiba("A program mappája nem állapítható meg.");
        var ideiglenes = Path.Combine(mappa, "AsiairAllomas.uj.exe");
        try
        {
            await LetoltAsync(uj, ideiglenes, haladas, megszakit);
            await EllenorizAsync(uj, ideiglenes, megszakit);
            Csere(ideiglenes);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            Takarit(ideiglenes);
            throw new FrissitesHiba("A program mappája nem írható. Tedd például a Dokumentumok mappába, és próbáld újra.");
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException && !megszakit.IsCancellationRequested)
        {
            Takarit(ideiglenes);
            throw new FrissitesHiba("A letöltés megszakadt. Próbáld újra.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Takarit(ideiglenes);
            throw new FrissitesHiba("Az új verzió nem indítható, talán a vírusirtó tiltja. A régi verzió maradt.");
        }
        catch
        {
            Takarit(ideiglenes);
            throw;
        }
    }

    public static async Task RegiTorolAsync()
    {
        for (var i = 0; i < 15; i++)
        {
            try
            {
                if (!File.Exists(Regi)) return;
                File.Delete(Regi);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    public void Dispose() => _kliens.Dispose();

    private static UjVerzio? Ertelmez(JsonElement kiadas)
    {
        var cimke = kiadas.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(cimke.TrimStart('v'), out var verzio)) return null;
        verzio = Normal(verzio);
        if (verzio <= Jelenlegi) return null;

        foreach (var csomag in kiadas.GetProperty("assets").EnumerateArray())
        {
            if (csomag.GetProperty("name").GetString() != CsomagNev) continue;
            string? sha = null;
            if (csomag.TryGetProperty("digest", out var ujjlenyomat) && ujjlenyomat.GetString() is { } szoveg &&
                szoveg.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                sha = szoveg["sha256:".Length..];
            return new UjVerzio(verzio, new Uri(csomag.GetProperty("browser_download_url").GetString()!),
                csomag.GetProperty("size").GetInt64(), sha);
        }

        return null;
    }

    private async Task LetoltAsync(UjVerzio uj, string cel, IProgress<int> haladas, CancellationToken megszakit)
    {
        using var valasz = await _kliens.GetAsync(uj.Letoltes, HttpCompletionOption.ResponseHeadersRead, megszakit);
        if (!valasz.IsSuccessStatusCode)
            throw new FrissitesHiba($"A letöltés nem sikerült ({(int)valasz.StatusCode}). Később próbáld újra.");
        await using var forras = await valasz.Content.ReadAsStreamAsync(megszakit);
        await using var fajl = new FileStream(cel, FileMode.Create, FileAccess.Write, FileShare.None);
        var puffer = new byte[81920];
        long kesz = 0;
        var utolso = -1;
        int olvasott;
        while ((olvasott = await forras.ReadAsync(puffer, megszakit)) > 0)
        {
            await fajl.WriteAsync(puffer.AsMemory(0, olvasott), megszakit);
            kesz += olvasott;
            var szazalek = uj.Meret > 0 ? (int)(kesz * 100 / uj.Meret) : 0;
            if (szazalek == utolso) continue;
            utolso = szazalek;
            haladas.Report(szazalek);
        }
    }

    private static async Task EllenorizAsync(UjVerzio uj, string fajl, CancellationToken megszakit)
    {
        if (new FileInfo(fajl).Length != uj.Meret)
            throw new FrissitesHiba("A letöltött fájl mérete nem egyezik. A régi verzió maradt.");
        if (uj.Sha256 is { } vart)
        {
            await using var folyam = File.OpenRead(fajl);
            var szamolt = Convert.ToHexString(await SHA256.HashDataAsync(folyam, megszakit));
            if (!szamolt.Equals(vart, StringComparison.OrdinalIgnoreCase))
                throw new FrissitesHiba("A letöltött fájl sérült. A régi verzió maradt.");
        }

        using var proba = Process.Start(new ProcessStartInfo(fajl, "--proba") { UseShellExecute = false })
                          ?? throw new FrissitesHiba("Az új verzió nem indul el. A régi verzió maradt.");
        using var idozito = CancellationTokenSource.CreateLinkedTokenSource(megszakit);
        idozito.CancelAfter(ProbaIdo);
        try
        {
            await proba.WaitForExitAsync(idozito.Token);
        }
        catch (OperationCanceledException) when (!megszakit.IsCancellationRequested)
        {
            proba.Kill();
            throw new FrissitesHiba("Az új verzió nem válaszolt a próbán. A régi verzió maradt.");
        }

        if (proba.ExitCode != 0) throw new FrissitesHiba("Az új verzió elbukott a próbán. A régi verzió maradt.");
    }

    private static void Csere(string uj)
    {
        Takarit(Regi);
        File.Move(Program, Regi);
        try
        {
            File.Move(uj, Program);
            Process.Start(new ProcessStartInfo(Program) { UseShellExecute = false });
        }
        catch
        {
            Takarit(Program);
            File.Move(Regi, Program);
            throw;
        }
    }

    private static void Takarit(string fajl)
    {
        try
        {
            File.Delete(fajl);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static Version Normal(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
