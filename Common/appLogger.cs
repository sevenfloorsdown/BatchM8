/*
 * Copyright 2017 Christian Rivera
 */

using System;
using System.IO;
using System.Threading;

namespace sevenfloorsdown
{
    public enum LogLevel
    {
        CRITICAL = 0,
        ERROR = 1,
        WARNING = 2,
        INFO = 3,
        DEBUG = 4,
        VERBOSE = 5
    };

    public class AppLoggerException : System.Exception
    {
        public AppLoggerException() : base() { }
        public AppLoggerException(string message) : base(message) { }
        public AppLoggerException(string message, System.Exception inner) : base(message, inner) { }

        protected AppLoggerException(System.Runtime.Serialization.SerializationInfo info,
                  System.Runtime.Serialization.StreamingContext context) { }
    }

    // -----------------------------------------------------------
    // Static class used for logging.
    // The logger automatically organizes directories by year, 
    // month then day. It then creates a log file per hour.
    // These are created in the /LOG directory wherever
    // the main executable is called.
    // -----------------------------------------------------------
    public class AppLogger
    {
        protected static String logNameBase;
        private static String errStr;

        public static LogLevel curLevel { get; set; }
        public static Boolean UseSubDirectories { get; set; }
        public static String exceptionMessage { get { return errStr; } }
        public static String LogPath { get; set; }
        public static String DateTimeFormat { get; set; }
        public static int MultipleAccessTimeInterval { get; set; }

        static AppLogger()
        {
            MultipleAccessTimeInterval = 300; // ms, arbitrary default
            curLevel = LogLevel.INFO;
            errStr = String.Empty;
        }

        public static LogLevel TranslateLogLevel(string level)
        {
            try
            {
                return (LogLevel)Enum.Parse(typeof(LogLevel), level.ToUpper());
            }
            catch (Exception e)
            {
                return LogLevel.INFO;
            }

        }

        private static String getDatedLogPath()
        {
            if (UseSubDirectories)
                return "/" + DateTime.Now.Year.ToString() + "/"
                           + DateTime.Now.Month.ToString("D2") + "/"
                           + DateTime.Now.Day.ToString("D2") + "/";
            else
                return "";
        }

        private static String getNewBase()
        {
            return "/" + logNameBase + "_" + DateTime.Now.Year.ToString()
                           + DateTime.Now.Month.ToString("D2")
                           + DateTime.Now.Day.ToString("D2") + "_" + DateTime.Now.TimeOfDay.Hours.ToString("D2") + "00.log";
        }

        public static void start(String logPath, String nameBase, String dateTimeFormat = "dd/MM/yyyy hh:mm:ss.fff", Boolean LogIntoSubDirectories = false)
        {
            LogPath = logPath + "/LOG";
            logNameBase = nameBase;
            DateTimeFormat = dateTimeFormat;
            UseSubDirectories = LogIntoSubDirectories;
            Log(LogLevel.INFO, "START");
        }

        private static String exceptionString(Exception e)
        {
            String eMsg = e.Message;
            if (curLevel >= LogLevel.DEBUG)
                eMsg += (Environment.NewLine + e.ToString());
            return eMsg;
        }

        // ----------------------------------------------
        //       Call this method to log things
        // ----------------------------------------------
        public static void Log(LogLevel level, String msg)
        {
            String finalPath = LogPath + getDatedLogPath();
            DirectoryInfo dr = null;
            if (Directory.Exists(finalPath))
                dr = new DirectoryInfo(finalPath);
            else
                try
                {
                    dr = Directory.CreateDirectory(finalPath);
                }
                // we really can't do much except report it so we 
                // don't have to distinguish which exception it is
                catch (Exception e)
                {
                    errStr = exceptionString(e);
                }

            if (dr != null)
                try
                {
                    EventWaitHandle waitHandle = new EventWaitHandle(true, EventResetMode.AutoReset, "APPWIDE_LOGFILE");
                    waitHandle.WaitOne(MultipleAccessTimeInterval); // Check if other processes are using the filename first and wait for it to let go
                    using (StreamWriter w = File.AppendText(finalPath + getNewBase()))
                    {
                        if (level <= curLevel)
                            try
                            {
                                w.WriteLine(String.Format("{0} {1}: {2}", DateTime.Now.ToString(DateTimeFormat), level.ToString(), msg));
                                waitHandle.Set(); // ok, done with the filename; tell others it's ok
                            }
                            catch (Exception e)
                            {
                                errStr = exceptionString(e);
                            }
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("The process cannot access the file") &&
                        e.Message.Contains("because it is being used by another process"))
                        errStr = String.Empty;
                    else
                        throw new AppLoggerException(e.Message);
                }
            // TODO: Have a more elegant but still general way of reporting logging error (whether WPF or web)
            if (errStr != String.Empty) throw new AppLoggerException(errStr);
        }

        public static void stop()
        {
            Log(LogLevel.INFO, "STOP");
        }
    }
}
