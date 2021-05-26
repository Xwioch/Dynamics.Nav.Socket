using Microsoft.Dynamics.Framework.UI.Extensibility;
using Microsoft.Dynamics.Framework.UI.Extensibility.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Dynamics.Nav.SocketServer
{
    [ControlAddInExport("Dynamics.Nav.Socket")]
    public class SocketServer : WinFormsControlAddInBase
    {
        private static int CONNECT_QUEUE_LENGTH = 100;

        private Label _internalControl;

        private List<bool> waitRead = new List<bool>();
        private int readCount = 0;
        private bool _enableDebug;

        private bool _serverSet;
        private bool _startServer;
        private string _ipAddress = null;
        private int _port;
        Thread ThreadSocket1;
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        private string _globalContent;

        public SocketServer()
        {

        }

        // Creo un fake control addin vuoto
        protected override Control CreateControl()
        {
            if (this._internalControl == null)
            {
                Label label = new Label();
                label.Visible = false;
                label.Size = Size.Empty;
                label.MinimumSize = Size.Empty;
                label.MaximumSize = Size.Empty;
                label.Margin = Padding.Empty;
                this._internalControl = label;
                this._internalControl.ParentChanged += new EventHandler(this.NativeControlParentChanged);
            }
            return (Control)this._internalControl;
        }

        private void NativeControlParentChanged(object sender, EventArgs e)
        {
            if (this._internalControl == null || this._internalControl.Parent == null)
                return;

            this._internalControl.ParentChanged -= new EventHandler(this.NativeControlParentChanged);
            MethodInvoker addInReady = this.AddInReady;

            if (addInReady == null)
                return;

            // Scatta quando il control panel (la page chiamante) diventa pronta quindi scatta l'AddInReady
            addInReady();
        }

        private void TriggerSocketException()
        {
            MethodInvoker SocketException = this.SocketException;

            if (SocketException == null)
                return;

            SocketException();
        }

        private void ListenForRequests()
        {
            byte[] bytes = new Byte[1024];

            IPAddress ipaddress = IPAddress.Parse(_ipAddress);
            IPEndPoint localEndPoint = new IPEndPoint(ipaddress, _port);

            Socket listener = new Socket(ipaddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(CONNECT_QUEUE_LENGTH);

                while (_startServer)
                {
                    if (_enableDebug)
                        MessageBox.Show("attesa socket 1");

                    allDone.Reset();

                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    allDone.WaitOne();
                }

                listener.Close();
                listener.Dispose();
            }
            catch (Exception e)
            {
                TriggerSocketException();
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                allDone.Set();

                if (!_startServer)
                    return;

                if (_enableDebug)
                    MessageBox.Show("chiamata socket 1");

                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                StateObject state = new StateObject();
                state.workSocket = handler;
                state.readCount = readCount;
                readCount++;

                waitRead.Add(false);

                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

                while (_startServer && !waitRead[state.readCount])
                {
                    Thread.Sleep(250);
                }

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
                handler.Dispose();
            }
            catch (Exception ex)
            {
                if (_enableDebug)
                    MessageBox.Show("eccezione apertura chiamata 1 " + ex.ToString());
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            try
            {
                if (!_startServer)
                    return;

                String content = String.Empty;

                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    content = state.sb.ToString();

                    if (content.IndexOf("<EOF>") > -1)
                    {
                        if (!string.IsNullOrEmpty(content))
                        {
                            if (_enableDebug)
                                MessageBox.Show("data received socket 1 start");

                            _globalContent = content;

                            MethodInvoker DataReceived = this.DataReceived;

                            if (DataReceived == null)
                            {
                                if (_enableDebug)
                                    MessageBox.Show("data received socket 1 null");
                                //return;
                            }
                            else
                            {
                                if (_enableDebug)
                                    MessageBox.Show("data received socket 1 execute");

                                DataReceived();
                            }

                            if (_enableDebug)
                                MessageBox.Show("data received socket 1 stop");
                        }

                        //handler.Shutdown(SocketShutdown.Both);
                        //handler.Close();
                        waitRead[state.readCount] = true;
                    }
                    else
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_enableDebug)
                    MessageBox.Show("eccezione lettura chiamata 1 " + ex.ToString());
            }
        }

        [ApplicationVisible]
        public void SetParameter(string IpAddress, int Port)
        {
            _ipAddress = IpAddress;
            _port = Port;
            _serverSet = true;
        }

        [ApplicationVisible]
        public void StartServer()
        {
            try
            {
                // Faccio partire il thread 
                if (_serverSet)
                {
                    _startServer = true;

                    ThreadSocket1 = new Thread(new ThreadStart(ListenForRequests));

                    ThreadSocket1.IsBackground = true;
                    ThreadSocket1.Start();

                    if (_enableDebug)
                        MessageBox.Show("start socket 1");
                }
            }
            catch (Exception ex)
            {
                TriggerSocketException();
            }
        }

        [ApplicationVisible]
        public void StopServer()
        {
            try
            {
                if (_serverSet && _startServer)
                {
                    _startServer = false;

                    allDone.Set();

                    ThreadSocket1.Join();

                    if (_enableDebug)
                        MessageBox.Show("chiusa socket 1");
                }
            }
            catch (Exception ex)
            {
                if (_enableDebug)
                    MessageBox.Show("eccezione chiusura socket 1");
            }
        }

        [ApplicationVisible]
        public string GetContent
        {
            get
            {
                return _globalContent;
            }
        }

        [ApplicationVisible]
        public void EnableDebug(bool enableDebug)
        {
            _enableDebug = enableDebug;
        }

        // *** TRIGGER VISIBILI IN PAGE ***

        [ApplicationVisible]
        public event MethodInvoker AddInReady;

        [ApplicationVisible]
        public event MethodInvoker DataReceived;

        [ApplicationVisible]
        public event MethodInvoker SocketException;
    }

    public class StateObject
    {
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
        public Socket workSocket = null;
        public int readCount = 0;
    }
}