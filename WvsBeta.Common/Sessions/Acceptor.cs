using System;
using System.Net;
using System.Net.Sockets;

namespace WvsBeta.Common.Sessions
{
    public abstract class Acceptor
    {
        public ushort Port { get; private set; }

        private TcpListener _listener;
        private TcpListener _listener6;

        protected Acceptor(ushort pPort)
        {
            Port = pPort;
            Start();
        }

        private bool Stopped = true;

        public void Start()
        {
            if (!Stopped) return;

            // IPv6 on Mono binds on IPv4 too.
            if (Type.GetType("Mono.Runtime") == null)
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start(200);
            }

            try
            {
                _listener6 = new TcpListener(IPAddress.IPv6Any, Port);
                _listener6.Start(200);
            }
            catch (Exception ex)
            {
                if (_listener == null)
                {
                    Console.WriteLine("Unable to bind on ipv6 nor ipv4!");
                    // Rethrow exception
                    throw;
                }

                // As long as one of them is started, its OK.
            }

            Stopped = false;
            _listener?.BeginAcceptSocket(EndAccept, _listener);
            _listener6?.BeginAcceptSocket(EndAccept, _listener6);
        }

        public void Stop()
        {
            if (Stopped) return;
            Stopped = true;

            _listener?.Stop();
            _listener = null;

            _listener6?.Stop();
            _listener6 = null;
        }

        private void EndAccept(IAsyncResult pIAR)
        {
            if (Stopped) return;

            var listener = (TcpListener)pIAR.AsyncState;

            try
            {
                var socket = listener.EndAcceptSocket(pIAR);
                if (PreAccept(socket, out var srcEndPoint, out var dstEndPoint))
                {
                    OnAccept(socket, srcEndPoint, dstEndPoint);
                }
                else
                {
                    // Declined, DC
                    try { socket.Shutdown(SocketShutdown.Both); } catch { }
                    try { socket.Disconnect(false); } catch { }
                    try { socket.Close(); } catch { }
                }
            }
            catch { }

            if (Stopped) return;
            listener?.BeginAcceptSocket(EndAccept, listener);
        }


        public virtual void OnAccept(Socket pSocket)
        {
            throw new NotImplementedException();
        }

        public virtual void OnAccept(Socket pSocket, IPEndPoint srcEndPoint, IPEndPoint dstEndPoint)
        {
            OnAccept(pSocket);
        }

        public virtual bool PreAccept(Socket pSocket, out IPEndPoint srcEndPoint, out IPEndPoint dstEndPoint)
        {
            srcEndPoint = pSocket.RemoteEndPoint as IPEndPoint;
            dstEndPoint = pSocket.LocalEndPoint as IPEndPoint;
            return true;
        }
    }
}