using OpenTK.Windowing.Desktop;

namespace Mars;

class Program
{
    static void Main(string[] args)
    {
        // MEGDR Data Online: https://pds-geosciences.wustl.edu/missions/mgs/megdr.html
        using var window = new HeightmapGame();
        window.Run();
    }
}
