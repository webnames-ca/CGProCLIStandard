using System;
using System.Collections.Generic;
using System.Diagnostics;

using WNStandard;

namespace CGProCLIStandardTests_NETFramework
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var oStopWatch = Stopwatch.StartNew();

            try
            {
                Test();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
            finally
            {
                oStopWatch.Stop();
                Console.WriteLine($"\nProgram completed in {oStopWatch.Elapsed.TotalSeconds:0.00}s.");
                Console.Write("Press any key to continue..."); Console.ReadKey();
            }
        }

        private static void Test()
        {
            var oCGProCLI = new CGProCLI.Connection("10.9.9.9", 106, "myuser", "mypassword", bSendPasswordWithAPOP: true);

            var sDomainName = "wntest20241205062218.ca";
            var sSuspendedDomainName = "susp_" + sDomainName;

            var sEmailAddress = $"test1@{sDomainName}";

            var dicAccounts = oCGProCLI.ListAccounts(sDomainName);
            Console.WriteLine($"{oCGProCLI.LastSubmissionLog.JsonSerializeForLogging()}\r\n{dicAccounts.JsonSerializeForLogging()}");

            var iStorageUsedBytes = oCGProCLI.GetAccountStorageUsed(sEmailAddress);
            Console.WriteLine($"{oCGProCLI.LastSubmissionLog.JsonSerializeForLogging()}\r\n{iStorageUsedBytes}");

            var dicEffectiveSettings = oCGProCLI.GetAccountEffectiveSettings(sEmailAddress);
            Console.WriteLine($"{oCGProCLI.LastSubmissionLog.JsonSerializeForLogging()}\r\n{dicEffectiveSettings.JsonSerializeForLogging()}");

            var aRules = oCGProCLI.GetAccountMailRules(sEmailAddress);
            Console.WriteLine($"{oCGProCLI.LastSubmissionLog.JsonSerializeForLogging()}\r\n{aRules.JsonSerializeForLogging()}");

            var dicDomainSettings = oCGProCLI.GetDomainSettings(sDomainName);
            Console.WriteLine($"{oCGProCLI.LastSubmissionLog.JsonSerializeForLogging()}\r\n{dicDomainSettings.JsonSerializeForLogging()}");

            var dicDomainEffectiveSettings = oCGProCLI.GetDomainEffectiveSettings(sDomainName);
            Console.WriteLine($"{oCGProCLI.LastSubmissionLog.JsonSerializeForLogging()}\r\n{dicDomainEffectiveSettings.JsonSerializeForLogging()}");

            oCGProCLI.RenameDomain(sDomainName, sSuspendedDomainName);
            oCGProCLI.RenameDomain(sSuspendedDomainName, sDomainName);

            Console.WriteLine($"Successfully renamed domain from {sDomainName} to {sSuspendedDomainName} and back again.");

            oCGProCLI.UpdateDomainSettings(sDomainName, new Dictionary<string, object> { { "wn_MaximumAliases", "2" }, { "wn_MaximumGroups", 3 } });
            Console.WriteLine($"{oCGProCLI.LastSubmissionLog.JsonSerializeForLogging()}\r\n");
        }
    }
}
