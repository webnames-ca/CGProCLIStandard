/*
MIT LICENSE

Copyright (c) 2024 Webnames.ca Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy 
of this software and associated documentation files (the “Software”), to deal 
in the Software without restriction, including without limitation the rights 
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
copies of the Software, and to permit persons to whom the Software is furnished
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
THE SOFTWARE.

 Author: Jordan Rieger
         Software Development Manager - Webnames.ca Inc.
         jordan@webnames.ca
         www.webnames.ca
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

using Antlr4.Runtime;
using Microsoft.Extensions.Logging;

using CGProCLI.CGProCLI;

using WNStandard;

namespace CGProCLI
{
    /// <summary>
    /// Manages connections to the MailSPEC Communigate Pro API server via the CLI/PWD interface.
    /// See https://support.mailspec.com/en/guides/communigate-pro-manual/cli-access.
    /// </summary>
    public class Connection : IDisposable
    {
        protected TcpClient _TcpClient = new TcpClient();
        protected StreamReader _StreamReader;
        protected StreamWriter _StreamWriter;
        protected ILogger _Logger;
        protected string _sHostNameOrIPAddress;

        protected SubmissionLog _oLastSubmissionLog;

        public const int i_DEFAULT_SEND_RECEIVE_TIMEOUT_MILLISECONDS = 100000;

        /// <summary>
        /// Gets or sets the send and receive timeout in milliseconds. The default is 100,000 (100 seconds.)
        /// </summary>
        public int TimeoutMilliseconds
        {
            get
            {
                return _TcpClient.ReceiveTimeout;
            }
            set
            {
                _TcpClient.ReceiveTimeout = value;
                _TcpClient.SendTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the ILogger object used by this connection.
        /// </summary>
        public ILogger Logger
        {
            get
            {
                return _Logger;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                _Logger = value;
            }
        }

        /// <summary>
        /// Returns the last SubmissionLog object with the request and raw response from server.
        /// </summary>
        public SubmissionLog LastSubmissionLog
        {
            get
            {
                return _oLastSubmissionLog;
            }
        }

        public enum ResponseCodes
        {
            OK = 200,
            OKDataProvided = 201,
            OKPleaseProvideData = 300,
            DomainAlreadyExists = 500,
            InsufficientAccessRights = 510,
            UnknownDomain = 512,
            UnknownUser = 513,
            AccountAlreadyExists = 520,
            GroupAlreadyExists = 523,
            ForwarderAlreadyExists = 524,
            MailboxAlreadyExists = 532,
            UnknownForwarder = 553,
            AccountInUse = 555
        }

        /// <summary>
        /// Creates a new CLI instance and connects using the specified parameters.
        /// </summary>
        /// <param name="oLogger">Logger to use. If null, a default console logger with the category 'CGProCLIAPI' is used.</param>
        /// <param name="bSendPasswordWithAPOP">APOP is a more secure way of transmitting the password via MD5 hash rather than cleartext.</param>
        public Connection(string sHostNameOrIPAddress, int iPort, string sUsername, string sPassword, bool bSendPasswordWithAPOP = false, ILogger oLogger = null)
        {
            if (oLogger == null)
            {
                ILoggerFactory ilLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                _Logger = ilLoggerFactory.CreateLogger("CGProCLIAPI");
            }
            else
            {
                _Logger = oLogger;
            }

            var sbInfoLog = new StringBuilder($"HostOrIP: {sHostNameOrIPAddress}; Port: {iPort}; Username: {sUsername}; APOP: {bSendPasswordWithAPOP}. ");

            try
            {
                _sHostNameOrIPAddress = sHostNameOrIPAddress;

                TimeoutMilliseconds = i_DEFAULT_SEND_RECEIVE_TIMEOUT_MILLISECONDS;

                _TcpClient.Connect(sHostNameOrIPAddress, iPort);
                _StreamReader = new StreamReader(_TcpClient.GetStream(), Encoding.ASCII);
                _StreamWriter = new StreamWriter(_TcpClient.GetStream(), Encoding.ASCII);

                sbInfoLog.Append($"Connected. Reading server greeting... ");

                // Example server greeting:
                // 200 mymail1.webnames.ca CommuniGate Pro PWD Server 7.1.10 ready <50.1733950486@mymail1.webnames.ca>
                var sServerGreeting = _StreamReader.ReadLine();

                var oResponse = Response.Parse(sServerGreeting);
                if (oResponse.ResponseCode != (int)ResponseCodes.OK)
                    throw new Exception($"Unexpected server greeting: {sServerGreeting}");

                sbInfoLog.Append($"Got greeting: '{sServerGreeting}'. ");

                if (bSendPasswordWithAPOP)
                {
                    sbInfoLog.Append("Logging in with APOP hash... ");

                    // APOP is a more secure way of transmitting the password via MD5 hash rather than cleartext.
                    // It requires hashing a combination of a part of the server greeting and the plaintext password.

                    var sSessionID = "";
                    for (var i = 0; i < oResponse.Data.ChildCount; ++i)
                    {
                        // This last part of the server greeting is what I call the session ID, e.g.:
                        // <50.1733950486@mymail1.webnames.ca>
                        sSessionID = oResponse.Data.GetChild(i).GetText();
                        if ((sSessionID?.StartsWith("<")).GetValueOrDefault())
                            break;
                    }

                    var abHash = Encoding.ASCII.GetBytes(sSessionID + sPassword); // Hash is based on concatenating the session ID and the plaintext password.
                    abHash = MD5.Create().ComputeHash(abHash);
                    var sHash = BitConverter.ToString(abHash).Replace("-", "").ToLower();

                    oResponse = SendAndParseResponse($"APOP {sUsername.CLIEncode()} {sHash.CLIEncode()}", bSuppressLog: true);

                    if (oResponse.ResponseCode != (int)ResponseCodes.OK)
                        throw new Exception($"API user {sUsername} APOP hash not accepted: {oResponse.RawData}");
                }
                else
                {
                    sbInfoLog.Append("Logging in with plaintext credentials... ");

                    oResponse = SendAndParseResponse($"USER {sUsername.CLIEncode()}");

                    if (oResponse.ResponseCode != (int)ResponseCodes.OK && oResponse.ResponseCode != (int)ResponseCodes.OKPleaseProvideData)
                        throw new Exception($"API user {sUsername} login not allowed: {oResponse.RawData}");

                    oResponse = SendAndParseResponse($"PASS {sPassword.CLIEncode()}", bSuppressLog: true);

                    if (oResponse.ResponseCode != (int)ResponseCodes.OK)
                        throw new Exception($"API user {sUsername} password not accepted: {oResponse.RawData}");
                }

                oResponse = SendAndParseResponse("inline"); // Not sure if this is necessary, but our old API code does it.

                if (oResponse.ResponseCode != (int)ResponseCodes.OK)
                    throw new Exception($"Inline command mode not accepted. Error: {oResponse.RawData}");
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message} {sbInfoLog}", ex);
            }

            _Logger.LogInformation($"{sbInfoLog}");
        }

        /// <summary>
        /// Returns a dictionary of all email accounts under the specified domain.
        /// Key = account name (email address local part), e.g. "user" in "user@domain.tld".
        /// Value = account type, e.g. "macnt" for standard multi-mailbox accounts.
        /// </summary>
        public Dictionary<string, string> ListAccounts(string sDomainName)
        {
            return SendCommand<Dictionary<string, string>, CGProCLIParser.CliDictionaryContext>
            (
                $"ListAccounts {sDomainName.CLIEncode()}",
                sDomainName,
                sCommandTypeForLogging: "ListAccounts",
                fConversion: (cliDic) => cliDic.ParseDictionary().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()),
                aeAcceptableResponseCodes: new ResponseCodes[] { ResponseCodes.OK, ResponseCodes.OKDataProvided }
            );
        }

        /// <summary>
        /// Returns the effective settings of the specified account.
        /// </summary>
        public Dictionary<string, object> GetAccountEffectiveSettings(string sEmailAddress)
        {
            return SendCommand<Dictionary<string, object>, CGProCLIParser.CliDictionaryContext>
            (
                $"GetAccountEffectiveSettings {sEmailAddress.CLIEncode()}",
                sDomainNameForLogging: sEmailAddress.GetDomainFromEmail(),
                sCommandTypeForLogging: "GetAccountEffectiveSettings",
                fConversion: d => d.ParseDictionary(),
                aeAcceptableResponseCodes: new ResponseCodes[] { ResponseCodes.OK, ResponseCodes.OKDataProvided }
            );
        }
        /// <summary>
        /// Returns the effective settings of the specified domain.
        /// </summary>
        public Dictionary<string, object> GetDomainEffectiveSettings(string sDomainName)
        {
            return SendCommand<Dictionary<string, object>, CGProCLIParser.CliDictionaryContext>
            (
                $"GetDomainEffectiveSettings {sDomainName.CLIEncode()}",
                sDomainNameForLogging: sDomainName,
                sCommandTypeForLogging: "GetDomainEffectiveSettings",
                fConversion: d => d.ParseDictionary(),
                aeAcceptableResponseCodes: new ResponseCodes[] { ResponseCodes.OK, ResponseCodes.OKDataProvided }
            );
        }

        /// <summary>
        /// Returns the settings of the specified domain, or null if the domain does not exist.
        /// </summary>
        public Dictionary<string, object> GetDomainSettings(string sDomainName)
        {
            var oResult = SendAndParseResponse($"GetDomainSettings {sDomainName.CLIEncode()}",
                                               sDomainNameForLogging: sDomainName,
                                               sCommandTypeForLogging: "GetDomainSettings");

            if (oResult.ResponseCode == (int)ResponseCodes.UnknownDomain)
                return null;

            if (oResult.ResponseCode != (int)ResponseCodes.OKDataProvided)
                throw new Exception($"Unexpected response. sDomainName: {sDomainName}; oResult.RawData: {oResult.RawData}");

            return oResult.Data.GetFirstSubObjectOrDefault<CGProCLIParser.CliDictionaryContext>().ParseDictionary();
        }

        /// <summary>
        /// Renames the specified domain to the specified new domain name.
        /// </summary>
        public void RenameDomain(string sDomainName, string sNewDomainName)
        {
            SendCommand<CGProCLIParser.CliStringContext>($"RenameDomain {sDomainName.CLIEncode()} into {sNewDomainName.CLIEncode()}",
                                                         sDomainNameForLogging: sDomainName,
                                                         sCommandTypeForLogging: "RenameDomain",
                                                         aeAcceptableResponseCodes: new ResponseCodes[] { ResponseCodes.OK });
        }

        public void UpdateDomainSettings(string sDomainName, Dictionary<string, object> dicDomainSettings)
        {
            SendCommand<CGProCLIParser.CliStringContext>($"UpdateDomainSettings {sDomainName.CLIEncode()} {dicDomainSettings.EncodeObject()}",
                                                         sDomainNameForLogging: sDomainName,
                                                         sCommandTypeForLogging: "UpdateDomainSettings",
                                                         aeAcceptableResponseCodes: new ResponseCodes[] { ResponseCodes.OK });
        }

        /// <summary>
        /// Returns the rules in the specified account.
        /// </summary>
        public List<object> GetAccountRules(string sEmailAddress)
        {
            return SendCommand<List<object>, CGProCLIParser.CliArrayContext>
            (
                $"GetAccountRules {sEmailAddress.CLIEncode()}",
                sDomainNameForLogging: sEmailAddress.GetDomainFromEmail(),
                sCommandTypeForLogging: "GetAccountRules",
                fConversion: a => a.ParseArray(),
                aeAcceptableResponseCodes: new ResponseCodes[] { ResponseCodes.OK, ResponseCodes.OKDataProvided }
            );
        }

        /// <summary>
        /// Returns the amount of storage used by the specified account, in bytes.
        /// </summary>
        public Int64 GetAccountStorageUsed(string sEmailAddress)
        {
            return SendCommand<Int64, CGProCLIParser.CliStringContext>
            (
                $"GetAccountInfo {sEmailAddress.CLIEncode()} Key StorageUsed",
                sDomainNameForLogging: sEmailAddress.GetDomainFromEmail(),
                sCommandTypeForLogging: "GetAccountInfo",
                fConversion: (cliString) => Int64.Parse(cliString?.GetText().EmptyIfNothing()),
                aeAcceptableResponseCodes: new ResponseCodes[] { ResponseCodes.OK, ResponseCodes.OKDataProvided }
            );
        }

        /// <summary>
        /// Helper method to assist in sending the specified command and returning the parsed response as a string.
        /// </summary>
        /// <typeparam name="ExpectedResponseType">A type expected to be found as a child or descendant in the parsed response.</typeparam>
        /// <param name="sCommand">The command string to send.</param>
        /// <param name="sDomainNameForLogging">The domain name to log.</param>
        /// <param name="sCommandTypeForLogging">The command type to log.</param>
        /// <param name="bThrowOnNullOrNonMatchingType">Whether to throw an exception if the parsed response does not contain the specified type.</param>
        /// <param name="aeAcceptableResponseCodes">An array of acceptable response codes. If empty, any response code is acceptable.</param>
        public string SendCommandGetString(string sCommand, string sDomainNameForLogging = null, string sCommandTypeForLogging = "", bool bThrowOnNullOrNonMatchingType = true, ResponseCodes[] aeAcceptableResponseCodes = null)
        {
            return SendCommand<CGProCLIParser.CliStringContext>(sCommand, sDomainNameForLogging, sCommandTypeForLogging, bThrowOnNullOrNonMatchingType, aeAcceptableResponseCodes)?.GetText();
        }

        /// <summary>
        /// Helper method to assist in sending the specified command and returning the response parsed into the specified type, with error checking.
        /// </summary>
        /// <typeparam name="ExpectedResponseType">A type expected to be found as a child or descendant in the parsed response.</typeparam>
        /// <param name="sCommand">The command string to send.</param>
        /// <param name="sDomainNameForLogging">The domain name to log.</param>
        /// <param name="sCommandTypeForLogging">The command type to log.</param>
        /// <param name="bThrowOnNullOrNonMatchingType">Whether to throw an exception if the parsed response does not contain the specified type.</param>
        /// <param name="aeAcceptableResponseCodes">An array of acceptable response codes. If empty, any response code is acceptable.</param>
        public ExpectedResponseType SendCommand<ExpectedResponseType>(string sCommand, string sDomainNameForLogging = null, string sCommandTypeForLogging = "", bool bThrowOnNullOrNonMatchingType = true, ResponseCodes[] aeAcceptableResponseCodes = null)
        {
            return SendCommand<ExpectedResponseType, object>(sCommand, sDomainNameForLogging, sCommandTypeForLogging, bThrowOnNullOrNonMatchingType, null, aeAcceptableResponseCodes);
        }

        /// <summary>
        /// Helper method to assist in sending the specified command and returning the response parsed into the specified type, with error checking.
        /// </summary>
        /// <typeparam name="ExpectedResponseType">A type expected to be found as a child or descendant in the parsed response.</typeparam>
        /// <typeparam name="InternalResponseType">The type to convert to the ExpectedResponseType using the optional conversion function (see optional fConversion parameter.) Use Object if no conversion is needed.</typeparam>
        /// <param name="sCommand">The command string to send.</param>
        /// <param name="sDomainNameForLogging">The domain name to log.</param>
        /// <param name="sCommandTypeForLogging">The command type to log.</param>
        /// <param name="bThrowOnNullOrNonMatchingType">Whether to throw an exception if the parsed response does not contain the specified type.</param>
        /// <param name="fConversion">Optional function to convert InternalResponseType into ExpectedResponseType.</param>
        /// <param name="aeAcceptableResponseCodes">An array of acceptable response codes. If empty, any response code is acceptable.</param>
        public ExpectedResponseType SendCommand<ExpectedResponseType, InternalResponseType>(string sCommand, string sDomainNameForLogging = null, string sCommandTypeForLogging = "", bool bThrowOnNullOrNonMatchingType = true, Func<InternalResponseType, ExpectedResponseType> fConversion = null, ResponseCodes[] aeAcceptableResponseCodes = null)
        {
            Response oResponse = null;

            try
            {
                oResponse = SendAndParseResponse(sCommand, false, sDomainNameForLogging, sCommandTypeForLogging);

                if (oResponse == null && bThrowOnNullOrNonMatchingType)
                    throw new Exception("Response null.");

                if ((aeAcceptableResponseCodes?.Length).GetValueOrDefault() > 0)
                {
                    var iaeAcceptableResponseCodes = aeAcceptableResponseCodes.Select(e => (int)e);
                    if (!iaeAcceptableResponseCodes.Contains(oResponse.ResponseCode))
                        throw new Exception("Response code unacceptable.");
                }
                object oResult;

                if (fConversion == null)
                {
                    oResult = oResponse.Data.GetFirstSubObjectOrDefault<ExpectedResponseType>();
                }
                else
                {
                    oResult = fConversion(oResponse.Data.GetFirstSubObjectOrDefault<InternalResponseType>());
                }

                if (oResult == null && bThrowOnNullOrNonMatchingType)
                {
                    throw new Exception("Response null or unexpected type.");
                }

                return (ExpectedResponseType)oResult;
            }
            catch (Exception ex)
            {
                throw new Exception($"sCommand: {sCommand}; aeAcceptableResponseCodes: {aeAcceptableResponseCodes.JsonSerializeForLogging()}; oResponse.RawData: {oResponse?.RawData}", ex);
            }
        }

        public class SubmissionLog
        {
            public DateTime? DateTimeSent { get; set; }
            public string ServerHostNameOrIPAddress { get; set; }
            public string DomainName { get; set; }
            public string CommandType { get; set; }
            public string Request { get; set; }
            public string Response { get; set; }
            public DateTime? DateTimeReceived { get; set; }
        }

        protected Response SendAndParseResponse(string sRequest, bool bSuppressLog = false, string sDomainNameForLogging = null, string sCommandTypeForLogging = "")
        {
            sRequest += "\r\n";

            _oLastSubmissionLog = new SubmissionLog()
            {
                DateTimeSent = DateTime.Now,
                ServerHostNameOrIPAddress = _sHostNameOrIPAddress,
                DomainName = sDomainNameForLogging,
                CommandType = sCommandTypeForLogging.EmptyIfNothing(),
                Request = sRequest
            };

            try
            {
                _StreamWriter.Write(sRequest);
                _StreamWriter.Flush();
                _oLastSubmissionLog.Response = _StreamReader.ReadLine();
                _oLastSubmissionLog.DateTimeReceived = DateTime.Now;
                return Response.Parse(_oLastSubmissionLog.Response);
            }
            catch (Exception ex)
            {
                _oLastSubmissionLog.Response = $"{ex.ToString()}. Raw response: {_oLastSubmissionLog.Response}";
                if (!bSuppressLog)
                {
                    LogResponse(_oLastSubmissionLog, LogLevel.Error);
                    bSuppressLog = true;
                }

                throw;
            }
            finally
            {
                if (!bSuppressLog)
                    LogResponse(_oLastSubmissionLog, LogLevel.Information);
            }
        }

        protected void LogResponse(SubmissionLog oSubmissionLog, LogLevel eLogLevel)
        {
            _Logger.Log(eLogLevel, default(EventId), oSubmissionLog, null, (oLog, oException) => oSubmissionLog.Response);
        }

        public class Response
        {
            public int ResponseCode { get; set; }
            public CGProCLIParser.CliDataContext Data { get; set; }
            public string RawData { get; set; }

            public static Response Parse(string sResponse)
            {
                var oAntlrInputStream = new AntlrInputStream(sResponse);
                var oCGProCLILexer = new CGProCLILexer(oAntlrInputStream);
                var oAntlrCommonTokenStream = new CommonTokenStream(oCGProCLILexer);
                var oCGProCLIParser = new CGProCLIParser(oAntlrCommonTokenStream);
                var oErrorListener_Lexer = new ErrorListener<int>() { Name = "Lexer" };
                var oErrorListener_Parser = new ErrorListener<IToken>() { Name = "Parser" };
                oCGProCLILexer.AddErrorListener(oErrorListener_Lexer);
                oCGProCLIParser.AddErrorListener(oErrorListener_Parser);

                var oTree = oCGProCLIParser.cliData();

                if (oErrorListener_Lexer.HadError)
                {
                    throw new Exception(oErrorListener_Lexer.Error);
                }
                else if (oErrorListener_Parser.HadError)
                {
                    throw new Exception(oErrorListener_Parser.Error);
                }
                else
                {
                    return new Response() { ResponseCode = oTree.GetResultCode(), Data = oTree, RawData = sResponse };
                }
            }
        }

        private class ErrorListener<S> : ConsoleErrorListener<S>
        {
            public bool HadError;
            public string Name { get; set; }
            public string Error { get; set; }

            public override void SyntaxError(TextWriter twOutput, IRecognizer oRecognizer, S tOffendingSymbol, int iLine, int iCol, string sMessage, RecognitionException oRecognitionException)
            {
                HadError = true;
                Error = $"{Name} error on line {iLine} col {iCol}: {sMessage}; Offending Symbol: {tOffendingSymbol.ToString()}";
                base.SyntaxError(twOutput, oRecognizer, tOffendingSymbol, iLine, iCol, Error, oRecognitionException);
            }
        }

        public void Disconnect()
        {
            if (_TcpClient.Connected)
            {
                _StreamWriter.WriteLine("QUIT");
                _StreamWriter.Flush();
                _TcpClient.Close();
            }
        }

        public void Dispose()
        {
            Disconnect();

            _TcpClient?.Dispose();
        }
    }
}
