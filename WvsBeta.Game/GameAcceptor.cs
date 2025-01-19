using System.Net.Sockets;
using System.Net;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public class GameAcceptor : Acceptor
    {
        public GameAcceptor()
            : base(Server.Instance.Port)
        {
        }
        
        public override void OnAccept(Socket pSocket, IPEndPoint srcEndPoint, IPEndPoint dstEndPoint)
        {
            new ClientSocket(pSocket, srcEndPoint);
        }
    }
    public class GameHaProxyAcceptor : HaProxyAcceptor
    {
        public GameHaProxyAcceptor(params string[] allowedAddresses) : base((ushort)(Server.Instance.Port + 50000), allowedAddresses)
        {
        }

        public override void OnAccept(Socket pSocket, IPEndPoint srcEndPoint, IPEndPoint dstEndPoint)
        { 
            new ClientSocket(pSocket, srcEndPoint);
        }
    }
}
