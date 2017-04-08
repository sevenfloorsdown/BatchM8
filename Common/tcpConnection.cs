/*
 * Copyright 2017 Christian Rivera
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace sevenfloorsdown
{
    public enum SocketTransactorType { server, client };
    
    public delegate void TcpEventHandler(object sender, EventArgs e);

    public class StateObject
    {
        #region Fields
        public Socket workSocket = null;
        public byte[] buffer;
        public StringBuilder sb = new StringBuilder();
        #endregion

        public StateObject(int BufferSize)
        {
            buffer = new byte[BufferSize];
        }
    }

    public class TcpConnection
    {
        #region Fields
        public SocketTransactorType ConnectionType { get; set; }
        public String    ConnectionName    { get; set; }
        public String    IpAddress         { get; set; }
        public int       PortNumber        { get; set; }
        public Encoding  MessageEncoding   { get; set; }
        public int       MaxNumConnections { get; set; }
        public int       BufferSize        { get; set; }
        public int       NumReconnectRetries { get; set; }

        public String Response { get { return tcpResponse; } }
        public String Header   { get; set; }
        public String Footer   { get; set; }

        public byte[] ByteResponse { get { return tcpByteResponse; } }
        public byte[] ByteHeader { get; set; }
        public byte[] ByteFooter { get; set; }

        private byte[] tcpByteResponse;
        private String tcpResponse;
        private Socket tcpSocket;
        private bool UseBytes;
        private bool RunningInBG;
        private bool AbruptDisconnect;
        private int ReconnectRetry;
        private int currentNumConnections;
        #endregion

        #region Events
        private ManualResetEvent connectDone = new ManualResetEvent(false);
        private ManualResetEvent sendDone    = new ManualResetEvent(false);
        private ManualResetEvent allDone     = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);
        #endregion

        #region Delegates
        public event TcpEventHandler TcpConnected    = delegate { };
        public event TcpEventHandler TcpDisconnected = delegate { };
        public event TcpEventHandler DataReceived    = delegate { };
        public event TcpEventHandler GeneralUseEvent = delegate { }; // in case you want something for *anything* else you may think of
        #endregion

        #region Event Handlers
        protected virtual void OnTcpConnected(EventArgs e)    
        {
            TcpConnected?.Invoke(this, e);
            ReconnectRetry = 0;
            currentNumConnections += 1;
        }

        protected virtual void OnTcpDisconnected(EventArgs e) 
        {
            currentNumConnections -= 1;
            TcpDisconnected?.Invoke(this, e);
            if (AbruptDisconnect)
            {
                while (ReconnectRetry > 0)
                {
                    ReconnectRetry--;
                    OpenConnection();
                }
                AbruptDisconnect = false;
            }
        }
        protected virtual void OnDataReceived(EventArgs e)    { if (DataReceived    != null) DataReceived(this, e);    }
        protected virtual void OnGeneralUseEvent(EventArgs e) { if (GeneralUseEvent != null) GeneralUseEvent(this, e); }
        #endregion

        public TcpConnection(SocketTransactorType myConnection,
                             string     ipAddress = "0.0.0.0",
                             uint       portNum   = 0,
                             bool       useBytes  = false)
        {
            ConnectionType = myConnection;
            ConnectionName = "";
            IpAddress = ipAddress;
            PortNumber = Convert.ToInt32(portNum);
            UseBytes = useBytes;
            Header = String.Empty;
            Footer = String.Empty;
            RunningInBG = true;
            if (BufferSize == 0) BufferSize = 1024;

            MessageEncoding = Encoding.ASCII;  // default, for now
            if (MaxNumConnections < 1) MaxNumConnections = 10;            // arbitrary default
            NumReconnectRetries = 1;
            AbruptDisconnect = false;
            ReconnectRetry = NumReconnectRetries;
            currentNumConnections = 0;
            OpenConnection();
        }

        ~TcpConnection()
        {
            CloseConnection();
        }

        // use this if IP address and port were set after creation.
        public void OpenConnection()
        {
            Thread workerThread = new Thread(openTCPConnection);
            if (IpAddress != "0.0.0.0" && PortNumber != 0) workerThread.Start();
        }

        private void openTCPConnection()
        {
            string msg = IpAddress + ":" + PortNumber.ToString();
            if (ConnectionType == SocketTransactorType.server)
            {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), PortNumber);
                Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(MaxNumConnections);
                    while (RunningInBG)
                    {
                        allDone.Reset();
                        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                        allDone.WaitOne();
                    }
                    // bye-bye
                    if (listener.Connected) listener.Shutdown(SocketShutdown.Both);
                    listener.Close();
                    OnTcpDisconnected(EventArgs.Empty);
                    AppLogger.Log(LogLevel.VERBOSE,
                                  String.Format("{0} closed.", ConnectionType.ToString()));                  
                }
                catch (Exception e)
                {
                    AppLogger.Log(LogLevel.ERROR,
                                  String.Format("Problem {0} {1} opening connection: {2}",
                                                 ConnectionType.ToString(), msg, e.Message));
                }              
            }
            else if (ConnectionType == SocketTransactorType.client)
            {
                string cxnMsg = String.Empty;
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(IpAddress), PortNumber);
                    tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    tcpSocket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), tcpSocket);                  
                    connectDone.WaitOne();
                    //OnTcpConnected(EventArgs.Empty);
                    AppLogger.Log(LogLevel.DEBUG,
                                  String.Format("{0} {1} {2}",
                                                 ConnectionType.ToString(),
                                                 msg, MessageEncoding.ToString()));
                    Receive(); // non-blocking due to callbacks
                    receiveDone.WaitOne();
                    // TODO: revisit these later to debug
                    //tcpSocket.Shutdown(SocketShutdown.Both);
                    //tcpSocket.Close();
                    //OnTcpDisconnected(EventArgs.Empty); 
                }
                catch (Exception e)
                {
                    AppLogger.Log(LogLevel.ERROR,
                                  String.Format("Error client connecting to {0}: {1}",
                                                 tcpSocket.RemoteEndPoint.ToString(),
                                                 e.Message));
                }
            }   
        }

        public void CloseConnection()
        {
            RunningInBG = false;
            allDone.Set();
            if (tcpSocket == null) return;
            if (tcpSocket.Connected) // this seems kludgey at the moment; maybe revisit later on?
            {
                AppLogger.Log(LogLevel.VERBOSE, String.Format("{0} {1} {2} closing.",
                    ConnectionType.ToString(),
                    ConnectionName,
                    tcpSocket.LocalEndPoint.ToString()));
                OnTcpDisconnected(EventArgs.Empty);// find ways of triggering this if we suddenly lose connection    
                tcpSocket.Shutdown(SocketShutdown.Both);
                tcpSocket.Close();
                tcpSocket.Dispose();
            }
        }
        
        private void AcceptCallback(IAsyncResult asyncResult)
        {
            if (currentNumConnections < MaxNumConnections)
            {
                Socket listener = (Socket)asyncResult.AsyncState;
                Socket handler = listener.EndAccept(asyncResult);
                allDone.Set();
                tcpSocket = handler; // for sending
                StateObject state = new StateObject(BufferSize);
                state.workSocket = handler;

                OnTcpConnected(EventArgs.Empty);
                handler.BeginReceive(state.buffer, 0, BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            else
            {
                AppLogger.Log(LogLevel.DEBUG,
                String.Format("{0} {1} {2}",
                               ConnectionType.ToString(),
                               "Exceeded max number of connections", MaxNumConnections.ToString()));
            }
        }

        private void ConnectCallback(IAsyncResult asyncResult)
        {
            string msg = String.Empty;
            try 
            {
                Socket client = (Socket)asyncResult.AsyncState;
                client.EndConnect(asyncResult);
                msg = client.LocalEndPoint.ToString();
                AppLogger.Log(LogLevel.DEBUG,
                              String.Format("{0}:{1} connected to {2}",
                                            ConnectionType.ToString() + " " + msg,
                                            ConnectionName,
                                            client.RemoteEndPoint.ToString()));
                AbruptDisconnect = false;
                OnTcpConnected(EventArgs.Empty);
                connectDone.Set();
            }
            catch (Exception e)
            {
                AppLogger.Log(LogLevel.ERROR,
                             String.Format("{0} connect callback: {1}",
                             ConnectionType.ToString() + " " + msg,
                             e.Message));
                OnTcpDisconnected(EventArgs.Empty);
            }
        }

        public void Send(byte[] msg)
        {
            byte[] toSend;
            string content = String.Empty;
            if (UseBytes)
            {
                toSend = new byte[ByteHeader.Length + msg.Length + ByteFooter.Length];
                ByteHeader.CopyTo(toSend, 0);
                msg.CopyTo(toSend, ByteHeader.Length);
                ByteFooter.CopyTo(toSend, ByteHeader.Length + msg.Length);
            }
            else
                toSend = msg;
            if (MessageEncoding == Encoding.ASCII)
                content = System.Text.Encoding.UTF8.GetString(toSend);
            try
            {
                if (tcpSocket == null)// && tcpSocket.Connected)
                {
                    AppLogger.Log(LogLevel.INFO,
                        String.Format("{0}:{1} No connected client",
                                ConnectionType.ToString() + " " + IpAddress + ":" + PortNumber.ToString(),
                                ConnectionName));
                    return;
                }
                tcpSocket.BeginSend(msg, 0, msg.Length, 0, new AsyncCallback(SendCallback), tcpSocket);
                AppLogger.Log(LogLevel.INFO,
                             String.Format("{0}:{1} sending {2}",
                                           ConnectionType.ToString() + " " + IpAddress + ":" + PortNumber.ToString(),
                                           ConnectionName,
                                           content));
            }
            catch (Exception e)
            { 
                AppLogger.Log(LogLevel.ERROR,
                              String.Format("{0}:{1} error sending {2} {3}",
                                           ConnectionType.ToString() + " " + IpAddress + ":" + PortNumber.ToString(),
                                           ConnectionName,
                                           content, e.Message));
            }
        }

        public void Send(String msg)
        {
            byte[] data = { 0 };
            if (MessageEncoding == Encoding.ASCII)
                data = Encoding.ASCII.GetBytes(Header + msg + Footer);
            Send(data);
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            string msg = String.Empty;
            try
            {
                Socket handler = (Socket)asyncResult.AsyncState;
                msg = handler.LocalEndPoint.ToString();
                int sentBytes = handler.EndSend(asyncResult);
                AppLogger.Log(LogLevel.VERBOSE,
                             String.Format("{0} sent {1} bytes",
                             ConnectionType.ToString() + " " + msg,
                             sentBytes.ToString()));
                if (ConnectionType == SocketTransactorType.client) sendDone.Set();
            }
            catch (Exception e)
            {
                AppLogger.Log(LogLevel.ERROR, 
                             String.Format("{0} send callback: {1}",
                             ConnectionType.ToString() + " " + msg,
                             e.Message));
            }
        }

        public void Receive()
        {
            string msg = String.Empty;
            try
            {
                StateObject state = new StateObject(BufferSize);
                state.workSocket = tcpSocket;
                msg = tcpSocket.LocalEndPoint.ToString();
                tcpSocket.BeginReceive(state.buffer, 0, BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                AppLogger.Log(LogLevel.ERROR,
                              String.Format("{0}:{1} error receiving {2}",
                                            ConnectionType.ToString() + " " + msg,
                                            ConnectionName,
                                            e.Message));
            } 
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            string msg = String.Empty;
            try
            {
                StateObject state = (StateObject)asyncResult.AsyncState;
                Socket handler = state.workSocket;
                msg = handler.LocalEndPoint.ToString();

                // TODO: Change this to a broadcast-type arrangement later on
                
                int bytesRead = handler.EndReceive(asyncResult);
                if (bytesRead > 0)
                {
                    if (!UseBytes)
                    {
                        if (MessageEncoding == Encoding.ASCII)
                            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                    }
                    if (ConnectionType == SocketTransactorType.client) 
                        handler.BeginReceive(state.buffer, 0, BufferSize, 0, new AsyncCallback(ReceiveCallback), state);

                    if (UseBytes)
                    { 
                        // remember to check if header/footer is empty
                        /*disable for now
                         * if (ByteHeader.SequenceEqual(state.buffer.Take(ByteHeader.Length)) &&
                            ByteFooter.Reverse().SequenceEqual(state.buffer.Reverse().Take(ByteFooter.Length)))
                            state.buffer.CopyTo(tcpByteResponse, 0);
                            OnDataReceived(EventArgs.Empty);*/
                        // TODO: the rest of the continue-or-stop-cuz-we-got-the-data-we-need code
                    }                   
                    else if (state.sb.Length > 1) 
                    {
                        string sbuf = state.sb.ToString();
                        int a = 0;
                        int b = 0; 
                        state.sb.Clear();
                        while (sbuf.Length > 0)
                        {
                            if (Header == String.Empty)
                                a = 0;
                            else
                                a = sbuf.IndexOf(Header) + Header.Length;
                            if (a < 0)
                                a = 0;
                            if (Footer == String.Empty)
                                b = sbuf.Length;
                            else
                                b = sbuf.IndexOf(Footer);
                            if (b < 0)
                                b = sbuf.Length;
                            b = b - a;
                            tcpResponse = sbuf.Substring(a, b);
                            if (b + a + Footer.Length < sbuf.Length)
                                sbuf = sbuf.Substring(b + a + Footer.Length);
                            else
                                sbuf = "";
                            try { 
                                OnDataReceived(EventArgs.Empty);
                            }
                            catch (Exception e)
                            {
                                /*AppLogger.Log(LogLevel.ERROR,
                                                String.Format("{0} receive callback: {1}",
                                                ConnectionType.ToString() + " " + msg, e.Message));*/
                                // ok, something's triggering this and I can't find it; seems to work
                                // fine though after it, tough shit. This whole thing needs to be
                                // rewritten.
                            }
                        }
                    }
                    if (ConnectionType == SocketTransactorType.server)
                        handler.BeginReceive(state.buffer, 0, BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    AbruptDisconnect = true;
                    ReconnectRetry = NumReconnectRetries;
                    OnTcpDisconnected(EventArgs.Empty);                   
                    AppLogger.Log(LogLevel.INFO, ConnectionType.ToString() + " " + msg + " disconnected");
                }
                receiveDone.Set();
            }
            catch (Exception e)
            {
                AppLogger.Log(LogLevel.ERROR,
                             String.Format("{0} receive callback: {1}",
                             ConnectionType.ToString() + " " + msg, e.Message));
            }
        }

        // Custom event that can be used for anything else the user may think of
        public void TriggerGeneralUseEvent() { OnGeneralUseEvent(EventArgs.Empty); }
    }
}
