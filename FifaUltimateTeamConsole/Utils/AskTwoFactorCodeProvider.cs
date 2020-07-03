using FutManagerLibrary.Configs;
using FutManagerLibrary.Interfaces;
using System.Threading.Tasks;

namespace FifaUltimateTeamConsole.Utils
{
    class AskTwoFactorCodeProvider : ITwoFactorCodeProvider
    {
        public AskTwoFactorCodeProvider()
        {

        }

        public Task<string> GetTwoFactorCodeAsync()
        {
            return Task.Run(() => "000000");
        }

        public SecFactorMode SecurityMode() => SecFactorMode.Ask; //use .Ask for user input BackupCode/Email code, for AppAuth user input code must setup AppAuth
    }
}
