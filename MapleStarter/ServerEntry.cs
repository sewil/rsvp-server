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

        public ServerBroadcast.LoginServer? SelectedLoginServer =>
            broadcast.LoginServers.FirstOrDefault(x => x.Started);

        public string? IP => SelectedLoginServer?.PublicIP;
        public ushort? Port => SelectedLoginServer?.Port;

        [DefaultValue(null)]
        public event EventHandler OnStart;

        [DefaultValue(null)]
        public event EventHandler OnShortCut;

        public ServerEntry(ServerBroadcast broadcast)
        {
            InitializeComponent();
            UpdateBroadcastInfo(broadcast);
        }

        public void UpdateBroadcastInfo(ServerBroadcast broadcast)
        {
            this.broadcast = broadcast;

            var loginServer = SelectedLoginServer;

            var ipText = "No LoginServers started...";
            if (loginServer != null)
            {
                ipText = $"{IP}:{Port}";
            }

            lblIP.Text = ipText;

            lblMachineName.Text = this.broadcast.MachineName;

            btnStart.Enabled = loginServer != null;
        }

        public bool IsSame(ServerBroadcast broadcast)
        {
            return this.broadcast.MachineName == broadcast.MachineName;
            //return this.broadcast.SentBy.Address.Equals(broadcast.SentBy.Address);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            OnStart.Invoke(this, e);
        }

        private void ServerEntry_Load(object sender, EventArgs e)
        {

        }

        private void btnShortcut_Click(object sender, EventArgs e)
        {
            OnShortCut.Invoke(this, e);
        }
    }
}
