﻿using System;
using System.Collections.Generic;
using System.Linq;
using Exceptron.Driver;
using NLog;
using NzbDrone.Common.Contract;

namespace NzbDrone.Common
{
    public static class ReportingService
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const string SERVICE_URL = "http://services.nzbdrone.com/reporting";
        private const string PARSE_URL = SERVICE_URL + "/ParseError";

        public static RestProvider RestProvider { get; set; }
        public static ExceptionClient ExceptronDriver { get; set; }


        private static readonly HashSet<string> parserErrorCache = new HashSet<string>();

        public static void ClearCache()
        {
            lock (parserErrorCache)
            {
                parserErrorCache.Clear();
            }
        }

        public static void ReportParseError(string title)
        {
            try
            {
                VerifyDependencies();

                lock (parserErrorCache)
                {
                    if (parserErrorCache.Contains(title.ToLower())) return;

                    parserErrorCache.Add(title.ToLower());
                }

                var report = new ParseErrorReport { Title = title };
                RestProvider.PostData(PARSE_URL, report);
            }
            catch (Exception e)
            {
                if (!EnvironmentProvider.IsProduction)
                {
                    throw;
                }

                e.Data.Add("title", title);
                logger.InfoException("Unable to report parse error", e);
            }
        }

        public static string ReportException(LogEventInfo logEvent)
        {
            try
            {
                VerifyDependencies();

                var exceptionData = new ExceptionData();

                exceptionData.Exception = logEvent.Exception;
                exceptionData.Component = logEvent.LoggerName;
                exceptionData.Message = logEvent.FormattedMessage;
                exceptionData.UserId = EnvironmentProvider.UGuid.ToString().Replace("-", string.Empty);

                if (logEvent.Level <= LogLevel.Info)
                {
                    exceptionData.Severity = ExceptionSeverity.None;
                }
                else if (logEvent.Level <= LogLevel.Warn)
                {
                    exceptionData.Severity = ExceptionSeverity.Warning;
                }
                else if (logEvent.Level <= LogLevel.Error)
                {
                    exceptionData.Severity = ExceptionSeverity.Error;
                }
                else if (logEvent.Level <= LogLevel.Fatal)
                {
                    exceptionData.Severity = ExceptionSeverity.Fatal;
                }

                return ExceptronDriver.SubmitException(exceptionData).RefId;
            }
            catch (Exception e)
            {
                if (!EnvironmentProvider.IsProduction)
                {
                    throw;
                }
                if (logEvent.LoggerName != logger.Name)//prevents a recursive loop.
                {
                    logger.WarnException("Unable to report exception. ", e);
                }
            }

            return null;
        }


        public static void SetupExceptronDriver()
        {
            ExceptronDriver = new ExceptionClient("CB230C312E5C4FF38B4FB9644B05E60G")
                                  {
                                      ApplicationVersion = new EnvironmentProvider().Version.ToString()
                                  };

            ExceptronDriver.ThrowsExceptions = !EnvironmentProvider.IsProduction;
            ExceptronDriver.Enviroment = EnvironmentProvider.IsProduction ? "Prod" : "Dev";
        }

        private static void VerifyDependencies()
        {
            if (RestProvider == null)
            {
                if (EnvironmentProvider.IsProduction)
                {
                    logger.Warn("Rest provider wasn't provided. creating new one!");
                    RestProvider = new RestProvider(new EnvironmentProvider());
                }
                else
                {
                    throw new InvalidOperationException("REST Provider wasn't configured correctly.");
                }
            }

            if (ExceptronDriver == null)
            {
                if (EnvironmentProvider.IsProduction)
                {
                    logger.Warn("Exceptron Driver wasn't provided. creating new one!");
                    SetupExceptronDriver();
                }
                else
                {
                    throw new InvalidOperationException("Exceptron Driver wasn't configured correctly.");
                }
            }
        }
    }
}
