using System;
using System.IO;
using DTSoundData;

namespace DreamTeamSoundToolConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string outFolder;
            if (args.Length > 0)
            {
                SoundDataArc arc = new SoundDataArc(args[0]);
                if (args.Length == 2)
                    outFolder = args[1];
                else
                    outFolder = Directory.GetCurrentDirectory();
                arc.extractArchive(outFolder);
                //Console.WriteLine(arc.buildArchive(outFolder, "SoundData_new.arc"));
                
            }
        }
    }
}
