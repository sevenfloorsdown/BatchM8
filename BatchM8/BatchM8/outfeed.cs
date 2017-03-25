/*
 * Copyright 2017 Christian Rivera
 * 
 */

using System;

namespace sevenfloorsdown
{
    public class OutFeed
    {
        private string _outputMessage;
        public SerialPortManager Port { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public string InfeedNum { get; set; }
        public string FixedAsciiData { get; set; }
        public int InputPLULength { get; set; }
        public int InputPPKLength { get; set; }
        public int OutputPLULength { get; set; }
        public int OutputPPKLength { get; set; }
        public string OutputMessage {
            get { return _outputMessage;  }
        }

        public OutFeed(SerialSettings settings)
        {
            Port = new SerialPortManager(settings.PortName)
            {
                Settings = settings
            };
            _outputMessage = string.Empty;
        }

        public string CreateOutputMessage(string inputMessage)
        {
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
            _outputMessage = InfeedNum + plu + ppk + FixedAsciiData;         
            return _outputMessage;
        }

        public void SendOutputMessage(string message)
        {
            Port.WriteLine(message);
        }

        public void SendOutputMessage()
        {
            Port.WriteLine(_outputMessage);
        }

    }
}
