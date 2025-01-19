using System.Linq;
using System.Net;
using System.Net.Sockets;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Login
{
    class LoginAcceptor : Acceptor
    {
        public LoginAcceptor() : base(Server.Instance.Port)
        {

        }

        public override void OnAccept(Socket pSocket, IPEndPoint srcEndPoint, IPEndPoint dstEndPoint)
        {
            new ClientSocket(pSocket, srcEndPoint);
        }
    }
    
    public class LoginHaProxyAcceptor : HaProxyAcceptor
    {
        public LoginHaProxyAcceptor(params string[] allowedAddresses) : base((ushort)(Server.Instance.Port + 50000), allowedAddresses)
        {
        }

        public override void OnAccept(Socket pSocket, IPEndPoint srcEndPoint, IPEndPoint dstEndPoint)
        { 
            new ClientSocket(pSocket, srcEndPoint);
        }
    }
}