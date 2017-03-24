﻿/*
 * Copyright 2017 Christian Rivera
 * 
 * System Overview: 
 * The existing Auto Labelling System is a legacy system provided by Accusort. The Sausage Line Auto Labelling System, as it is officially known, 
 * differs slightly to the Auto Warehouse Auto Labelling System, in that all the cartons with product within are completely sealed. Due to the sealing 
 * of the products it is impossible to identify the products within the cartons for application of shipping labels (SSCC labels). To combat this issue 
 * 'batch mode' was introduced as only one product can be producted at one time. In the case of this particular system though the requirement is that 
 * we feed the single auto labelling system with two infeed lines (e.g. 'dual batch mode'). This mode is currently handled through the use of a microntroller 
 * and application of infeed identifying barcodes. The identifying barcodes are applied to the outer top face of the closed cartons, thus provided which infeed 
 * line the product was packed from. With the product information (PLU and price per kg) provided by the 'piece' tray sealing machine it is now possible that 
 * we can process the 'dual batch mode' application. 
 * 
 * Current operation :
 * Two infeed lines that seal individual trays provide, via serial communications, the product information and the individual sell price. This information is 
 * transmitted currently to a Unitronics microncontroller. The controller holds the product information for each infeed line (of which there is only 2). For 
 * individual trays a message is received, and the holding product is only change once a differing product value is (PLU - product code; OR PPK - price per kg) received. 
 * The controller also accepts a third message whcih is the start of the current Auto Labelling system. The message is the scanned decoded barcode associated to each 
 * infeed line. For simplicity the barcode is either 01 or 02 (e.g. line 1 or 2).  Upon receiving either 01 or 02 message from the camera the controller then retrieves 
 * the held mesage for either 01 or 02 infeed line previously stored and then transmits the required protocol message to the Auto Labelling system. In this operational
 * method 01 or 02 can alternate and the Auto Labelling System will know which product is required for labelling without physically scanning the internals of the carton.
 * 
 * Required Software modification : 
 * Due to many points of failures with the microcontroller systems, and the lack of visibility of the operaiton the idea at this stage is to provide a simple intermediary 
 * software application that will bypass the requirement of the microcontroller. The software application is to operate on the same PC as the current Auto Labelling software. 
 * Software application is required to accept the two infeed line messages via serial under the protocol mentioned above. As per the current operation the infeed messages 
 * are to be held/locked upon correct format and not changed until a different message is received. Software is to provide a third input connection where the software will 
 * be a simple TCP Socket connection with connection type to be a 'server' and the camera the 'client'. Upon receiving messages from the camera in the correct format the 
 * software will extract as per the current operation the held product information for the message received from the camera (either 01 or 02) and transmit the correctly 
 * formulated string to the Auto Labelling system immediately.
 * 
 * Diagnostics  
 * A heartbeat will be provided from the camera connection ONLY not the infeed lines. All connection faults to be displayed on screen and/or log files.  
 */


using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;

namespace sevenfloorsdown
{
    class InFeed
    {
        private string inFeedDataBuffer = "";
        public SerialPortManager Port { get; set; }
        public string SwitchValue { get; set; }
        public string InFeedData { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public string MessageFormat { get; set; }

        public InFeed(SerialSettings settings, string switchValue)
        {
            Port = new SerialPortManager(settings.PortName);
            Port.Settings = settings;
            SwitchValue = switchValue;
        }
    }

    class BatchM8
    {
        static Mutex mutex = new Mutex(true, "1659aff2-7d2c-48f5-8557-a4efd694d16d");

        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        private static settingsJSONutils ini;
        private static List<InFeed> LineInFeeds;
        private static SerialPortManager LineOutFeed;
        private static TcpConnection     InFeedSwitch;

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        [STAThread]
        static void Main(string[] args)
        {
            // subsequent additional instances just quit running
            if (!mutex.WaitOne(TimeSpan.Zero, true)) Environment.Exit(0);

            HandlerRoutine hr = new HandlerRoutine(TermHandlerRoutine);

            GC.KeepAlive(hr);
            SetConsoleCtrlHandler(hr, true);

            string filePath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + "\\" + args[0];

            if (!File.Exists(filePath))
            {
                ErrorMessage(System.Reflection.Assembly.GetEntryAssembly().GetName().Name + "settings.json not found", false);
                Environment.Exit(0);
            }

            ini = new settingsJSONutils(filePath);

            AppLogger.Start(ini.GetSettingString("Root", "."),
                            ini.GetSettingString("LogFile", System.Reflection.Assembly.GetEntryAssembly().GetName().Name, "Logging"),
                            ini.GetSettingString("DateTimeFormat", "dd/MM/yyyy hh:mm:ss.fff", "Logging"));
            AppLogger.CurrentLevel = AppLogger.ConvertToLogLevel(ini.GetSettingString("LogLevel", "Verbose", "Logging"));

            if (InitializePorts())
            {

            }

            ProgramEnd();
        }

        public static bool InitializePorts()
        {
            SerialSettings comSettings;
            LineInFeeds = new List<InFeed>();
            string[] section = new string[] { "LineInFeed1", "LineInFeed2", "OutFeed" };
            for (int i = 1; i <= section.Length; i++)
            {
                int j = i - 1;
                PrintLog("Setting " + section[j]);
                try
                {
                    comSettings = new SerialSettings();
                    comSettings.PortName = ini.GetSettingString("COMPort", "COM" + i.ToString(), section[j]);
                    comSettings.BaudRate = ini.GetSettingInteger("Baudrate", 9600, section[j]);
                    comSettings.DataBits = ini.GetSettingInteger("Databits", 8, section[j]);
                    comSettings.Parity = (Parity)(Enum.Parse(typeof(Parity), ini.GetSettingString("Parity", "None", section[j])));
                    comSettings.StopBits = (StopBits)ini.GetSettingInteger("StopBits", 1, section[j]);
                    if (i < section.Length) // InFeed
                    {
                        InFeed tmpInFeed = new InFeed(comSettings, ini.GetSettingString("SwitchValue", "0" + i.ToString(), section[j]));
                        LineInFeeds.Add(tmpInFeed);
                        LineInFeeds[j].Header = StringUtils.ParseIntoASCII(ini.GetSettingString("Header", "", section[j]));
                        LineInFeeds[j].Footer = StringUtils.ParseIntoASCII(ini.GetSettingString("Footer", "", section[j]));
                        LineInFeeds[j].MessageFormat = ini.GetSettingString("MessageFormat", "", section[j]);
                        if (i == 1)
                            LineInFeeds[j].Port.NewSerialDataReceived += new EventHandler<SerialDataEventArgs>(LineInFeed1NewDataReceived);
                        if (i == 2)
                            LineInFeeds[j].Port.NewSerialDataReceived += new EventHandler<SerialDataEventArgs>(LineInFeed2NewDataReceived);
                    }
                    else  // OutFeed
                    {
                        LineOutFeed = new SerialPortManager(comSettings.PortName);
                        LineOutFeed.Settings = comSettings;
                        LineOutFeed.NewSerialDataReceived += new EventHandler<SerialDataEventArgs>(LineOutFeedNewDataReceived);
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage(String.Format("Failed setting serial {0} settings: {1}", section[j], e.Message));
                    return false;
                }
            }

            string _section = "InFeedSwitch";
            string switchStr = ini.GetSettingString("ShortDesc", "Infeed switch", _section);
            PrintLog("Setting " + switchStr);
            try
            {
                SocketTransactorType cxnType = (ini.GetSettingString("Sockettype", "SERVER", _section).ToUpper() == "SERVER") ?
                                                SocketTransactorType.server : SocketTransactorType.client;
                InFeedSwitch = new TcpConnection(cxnType,
                                   ini.GetSettingString("IPAddress", "0.0.0.0", _section),
                                   (uint)ini.GetSettingInteger("PortNumber", 0, _section))
                {
                    MaxNumConnections = ini.GetSettingInteger("MaxNumberConnections", 1, _section),
                    Header = StringUtils.ParseIntoASCII(ini.GetSettingString("Header", "", _section)),
                    Footer = StringUtils.ParseIntoASCII(ini.GetSettingString("Footer", "", _section))
                };
                InFeedSwitch.TcpConnected += new TcpEventHandler(InFeedSwitchConnectedListener);
                InFeedSwitch.TcpDisconnected += new TcpEventHandler(InFeedSwitchDisconnectedListener);
                InFeedSwitch.DataReceived += new TcpEventHandler(InFeedSwitchDataReceiver);
            }
            catch (Exception e)
            {
                ErrorMessage(String.Format("Failed setting {0} settings: {1}", switchStr, e.Message));
                return false;
            }
            return true;
        }

        static void LineInFeed1NewDataReceived(object sender, SerialDataEventArgs e) { LineInFeedCommonDataReceived(1, e); }
        static void LineInFeed2NewDataReceived(object sender, SerialDataEventArgs e) { LineInFeedCommonDataReceived(2, e); }

        static void LineInFeedCommonDataReceived(int index, SerialDataEventArgs e)
        {
            // collect data until delimiter comes in
            string outputAsText = BytesToString(e.Data);
            PrintLog("Received " + outputAsText);
        }

        static void LineOutFeedNewDataReceived(object sender, SerialDataEventArgs e)
        {
            /*string outputAsText = BytesToString(e.Data);
            serialBuffer.AddRange(new List<byte>(e.Data));
            printLog("Received " + outputAsText);*/
        }

        private static void InFeedSwitchConnectedListener(object sender, EventArgs e)
        {
            TcpConnection current = (TcpConnection)sender;
            if (current == null) return;
            PrintLog(String.Format("{0}: {1} connected", current.IpAddress.ToString(), current.PortNumber));
        }

        private static void InFeedSwitchDisconnectedListener(object sender, EventArgs e)
        {
            TcpConnection current = (TcpConnection)sender;
            if (current == null) return;
            PrintLog(String.Format("{0}: {1} disconnected", current.IpAddress.ToString(), current.PortNumber));

            PrintLog("Attempting to reconnect");
            InFeedSwitch.OpenConnection();
        }

        private static void InFeedSwitchDataReceiver(object sender, EventArgs e)
        {
            TcpConnection current = (TcpConnection)sender;
            if (current == null) return;
            string payload = current.Response;
            PrintLog(String.Format("{0}:{1} received {2}", current.IpAddress.ToString(), current.PortNumber, payload));
            ProcessInFeedSwitchMessage(payload);
        }

        private static void ProcessInFeedSwitchMessage(string payload)
        {

        }

        public static string BytesToString(byte[] rawData)
        {
            return BitConverter.ToString(rawData).Replace("-", " ");
        }

        // common error message thingy
        public static void ErrorMessage(string message, Boolean goLog = true)
        {
            Console.WriteLine(message);
            if (goLog) AppLogger.Log(LogLevel.ERROR, message);
            Console.ReadLine();
        }

        public static void PrintLog(string message, Boolean goLog = true)
        {
            Console.WriteLine(message);
            if (goLog) AppLogger.Log(LogLevel.INFO, message);
        }

        private static bool TermHandlerRoutine(CtrlTypes dwCtrlType)
        {
            switch (dwCtrlType)
            { //no break so all close events handled same
                case CtrlTypes.CTRL_CLOSE_EVENT:
                case CtrlTypes.CTRL_BREAK_EVENT:
                case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                case CtrlTypes.CTRL_C_EVENT:
                    //quitLoops = true;
                    ProgramEnd();
                    return true;
            }
            return false;
        }

        private static void ProgramEnd()
        {
            /*if (debuggerCxn != null)
                debuggerCxn.StopListening();
            BeaconList.Clear();
            dbCxn.CloseConnection();
            satCxn.CloseConnection();*/
            InFeedSwitch.CloseConnection();
            PrintLog("Exiting...");
            Environment.Exit(0);
        }
    }
}
