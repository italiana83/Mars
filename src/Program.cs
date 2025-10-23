using Mars;
using OpenTK.Windowing.Desktop;
using System;

namespace GravitationalWaveVisualizer
{
    class Program
    {
        static void Main(string[] args)
        {
            //MEGDR Data Online
            //https://pds-geosciences.wustl.edu/missions/mgs/megdr.html

            using (var window = new HeightmapGame())
            {
                window.Run();
            }
        }
    }
}