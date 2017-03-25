/*
 * Copyright 2017 Christian Rivera
 * 
 */

using System;
using System.Text.RegularExpressions;

namespace sevenfloorsdown
{
    public class InFeed
    {
        private string buffer = string.Empty;
        private Regex inRegex;
        private string pattern;
        public SerialPortManager Port { get; set; }
        public string SwitchValue { get; set; }
        public string InFeedData { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public bool CheckMessageFormat { get; set; }
        public int PLULength { get; set; }
        public int PPKLength { get; set; }
        public string MessageFormat {
            get { return pattern;  }
            set
            {
                pattern = value;
                inRegex = new Regex(value, RegexOptions.None);
            }
        }

        public InFeed(SerialSettings settings, string switchValue="")
        {
            Port = new SerialPortManager(settings.PortName)
            {
                Settings = settings
            };
            SwitchValue = switchValue;
        }

        public bool IsInCorrectFormat(string value)
        {
            if (pattern == String.Empty) return true;
            MatchCollection matches = inRegex.Matches(value);
            if (matches.Count != 1) return false;
            return true;
        }

        public bool BufferDataUpdated(string data)
        {
            int s = data.IndexOf(Header);
            int e = data.IndexOf(Footer);
            bool nobuf = buffer == string.Empty;

            if (s > -1)
            {
                if (e > -1 && e > s)
                {
                    string sub = data.Substring(s + 1, e - s - 1);
                    if (CheckMessageFormat && !IsInCorrectFormat(sub)) return false;
                    if (!String.IsNullOrEmpty(InFeedData))
                        if (InFeedData.Equals(sub)) return false;
                    InFeedData = sub;
                    buffer = string.Empty;
                    return true;
                }
                else if (e == -1)
                {
                    buffer = data.Substring(s+1);
                    return false;
                }
            }
            else
            {
                if (!nobuf)
                {
                    if (e > -1)
                    {
                        buffer += data.Substring(0, e);
                        if (CheckMessageFormat && !IsInCorrectFormat(buffer)) return false;
                        if (!String.IsNullOrEmpty(InFeedData))
                            if (InFeedData.Equals(buffer)) return false;
                        InFeedData = buffer;
                        buffer = string.Empty;
                        return true;
                    }
                }
                buffer += data;
                return false;  
            }
            return false;
        }
    }
}
