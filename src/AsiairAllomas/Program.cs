namespace AsiairAllomas;

internal static class Program
{
    [STAThread]
    private static int Main(string[] argumentumok)
    {
        if (argumentumok.Contains("--proba")) return 0;
        ApplicationConfiguration.Initialize();
        Application.Run(new FoAblak());
        return 0;
    }
}
