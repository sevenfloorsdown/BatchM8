/*
 * Copyright 2017 Christian Rivera
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sevenfloorsdown
{
    public class InFeed
    {
        private string buffer = string.Empty;
        public SerialPortManager Port { get; set; }
        public string SwitchValue { get; set; }
        public string InFeedData { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public string MessageFormat { get; set; }

        public InFeed(SerialSettings settings, string switchValue)
        {
            Port = new SerialPortManager(settings.PortName)
            {
                Settings = settings
            };
            SwitchValue = switchValue;
        }

        public bool BufferDataReady(string data)
        {
            int s = data.IndexOf(Header);
            int e = data.IndexOf(Footer);
            bool nobuf = buffer == string.Empty;

            if (s > -1)
            {
                if (e > -1 && e > s)
                {
                    InFeedData = data.Substring(s, e - s + 1);
                    // check message validity first!
                    buffer = string.Empty;
                    return true;
                }
                else if (e == -1)
                {
                    InFeedData = data.Substring(s);
                    return false;
                }
            }
            else
            {
                if (!nobuf)
                {
                    if (e > -1)
                    {
                        InFeedData = data.Substring(0, e + 1);
                        // check message validity first!
                        buffer = string.Empty;
                        return true;
                    }
                    else
                    {
                        buffer += data;
                        return false;
                    }
                }
            }
            return false;
        }
    }
}
