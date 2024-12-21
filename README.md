# CGProCLIStandard
## A C# .NET Standard library to manages connections to the MailSPEC Communigate Pro API server via the CLI/PWD interface
by Jordan Rieger  
Software Development Manager  
Webnames.ca Inc.  
www.webnames.ca  
jordan@webnames.ca  

## Notes
- Parsing is accomplished via an ANTLR4 grammar that I wrote, `CGProCLI.g4`.
- Logging is supported via an ILogger injected in the `Connection` constructor, or leave that null for the default console logger.
- TODO: Lots of commands not yet implemented, but most people won't need more than a handful of methods for their use case. With the helper and plumbing methods I've written, it should be easy to implement whatever commands you need.

## Dependenices
- .NET Standard 2.0 (so any .NET Core/5+/Framework app should work.)
- Antlr4BuildTasks
- Antlr4.Runtime.Standard
- Microsoft.Extensions.Logging
- Newtonsoft.Json (of course)
  
## Usage
See the test console app for an example (`CGProCLIStandardTests_NETFramework\Program.cs`):
```
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

var aRules = oCGProCLI.GetAccountRules(sEmailAddress);
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
```
