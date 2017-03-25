/*
 * Copyright 2017 Christian Rivera
 * 
 */

using System;

namespace sevenfloorsdown
{
    public class OutFeed
    {
        public SerialPortManager Port { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public string InfeedNum { get; set; }
        public string FixedAsciiData { get; set; }
        public int InputPLULength { get; set; }
        public int InputPPKLength { get; set; }
        public int OutputPLULength { get; set; }
        public int OutputPPKLength { get; set; }

        public OutFeed(SerialSettings settings)
        {
            Port = new SerialPortManager(settings.PortName)
            {
                Settings = settings
            };
        }

        public string CreateOutputMessage(string inputMessage)
        {
            string result = String.Empty;
            string plu = String.Empty;
            string ppk = String.Empty;
            try
            {
                plu = inputMessage.Substring(InputPLULength - OutputPLULength, OutputPLULength);
            }
            catch (Exception e)
            {
                throw new Exception(String.Format("Mismatch with input and output PLU lengths: {0} vs {1}, {2}", 
                    InputPLULength.ToString(), OutputPLULength.ToString(), e.Message));
            }
            try
            {
                ppk = inputMessage.Substring(InputPLULength + 1, OutputPPKLength);
            }
            catch (Exception e)
            {
                throw new Exception(String.Format("Mismatch with input and output PPK lengths: {0} vs {1}, {2}",
                    InputPPKLength.ToString(), OutputPPKLength.ToString(), e.Message));
            }
            result = InfeedNum + plu + ppk + FixedAsciiData;
            return result;
        }

    }
}
