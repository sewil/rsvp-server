using System.Net.Sockets;
using System.Net;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Shop
{
    class ShopAcceptor : Acceptor
    {
        public ShopAcceptor() : base(Server.Instance.Port)
        {
        }
        
        public override void OnAccept(Socket pSocket, IPEndPoint srcEndPoint, IPEndPoint dstEndPoint)
        {
            new ClientSocket(pSocket, srcEndPoint);
        }
    }
    
    public class ShopHaProxyAcceptor : HaProxyAcceptor
    {
        public ShopHaProxyAcceptor(params string[] allowedAddresses) : base((ushort)(Server.Instance.Port + 50000), allowedAddresses)
        {
        }

        public override void OnAccept(Socket pSocket, IPEndPoint srcEndPoint, IPEndPoint dstEndPoint)
        { 
            new ClientSocket(pSocket, srcEndPoint);
        }
    }
}
