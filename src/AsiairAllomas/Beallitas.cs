using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AsiairAllomas;

public sealed class Beallitas
{
    public string Felhasznalo { get; set; } = "picloud";
    public string? TitkosJelszo { get; set; }
    public bool VorosMod { get; set; }

    private static string Mappa =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsiairAllomas");

    private static string Fajl => Path.Combine(Mappa, "beallitas.json");

    public static Beallitas Betolt()
    {
        try
        {
            return JsonSerializer.Deserialize<Beallitas>(File.ReadAllText(Fajl)) ?? new Beallitas();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Beallitas();
        }
    }

    public void Ment()
    {
        Directory.CreateDirectory(Mappa);
        var ideiglenes = Fajl + ".uj";
        File.WriteAllText(ideiglenes, JsonSerializer.Serialize(this));
        File.Move(ideiglenes, Fajl, true);
    }

    public string? Jelszo()
    {
        if (string.IsNullOrEmpty(TitkosJelszo)) return null;
        try
        {
            var nyers = ProtectedData.Unprotect(Convert.FromBase64String(TitkosJelszo), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(nyers);
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public void JelszoBeallit(string jelszo)
    {
        var titkos = ProtectedData.Protect(Encoding.UTF8.GetBytes(jelszo), null, DataProtectionScope.CurrentUser);
        TitkosJelszo = Convert.ToBase64String(titkos);
    }
}
