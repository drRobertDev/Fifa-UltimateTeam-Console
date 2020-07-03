using FutManagerLibrary.Interfaces;
using System;

namespace FifaUltimateTeamConsole.Utils
{
    class Logger : ILogger
    {
        public void Log(string module, string text)
        {
            Console.WriteLine(module + " - " + text);
        }

        public void LogError(string module, string text)
        {
            Console.WriteLine(module + " - " + text);
        }

        public void LogWarning(string module, string text)
        {
            Console.WriteLine(module + " - " + text);
        }
    }
}
