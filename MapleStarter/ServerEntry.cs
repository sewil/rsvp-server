using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapleStarter
{
    public partial class ServerEntry : UserControl
    {
        private ServerBroadcast broadcast { get; set; }

        public string IP { get; private set; }
        public ushort Port { get; private set; }

        public event EventHandler OnStart;

        public ServerEntry(ServerBroadcast broadcast)
        {
            InitializeComponent();
            UpdateBroadcastInfo(broadcast);
        }

        public void UpdateBroadcastInfo(ServerBroadcast broadcast)
        {
            this.broadcast = broadcast;

            var firstStartedLoginServer = this.broadcast.LoginServers.FirstOrDefault(x => x.Started);

            lblIP.Text = firstStartedLoginServer?.PublicIP ?? "No LoginServers started...";
            lblMachineName.Text = this.broadcast.MachineName;

            btnStart.Enabled = firstStartedLoginServer != null;

            if (firstStartedLoginServer != null)
            {
                IP = firstStartedLoginServer.PublicIP;
                Port = firstStartedLoginServer.Port;
            }
        }

        public bool IsSame(ServerBroadcast broadcast)
        {
            return this.broadcast.SentBy.Address.Equals(broadcast.SentBy.Address);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            OnStart.Invoke(this, e);
        }

        private void ServerEntry_Load(object sender, EventArgs e)
        {

        }
    }
}
