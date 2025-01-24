using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WvsBeta.Launcher.Config;

namespace WvsBeta.Launcher
{
    public partial class LANMode : Form
    {
        private WvsConfig[] wvsServers;

        public LANMode(params WvsConfig[] wvsServers)
        {
            InitializeComponent();
            this.wvsServers = wvsServers;
        }

        public Interface? SelectedInterface => lbInterfaces.SelectedItem as Interface;

        public class Interface
        {
            public NetworkInterface iface { get; }

            public Interface(NetworkInterface iface)
            {
                this.iface = iface;
            }

            public IPAddress? MulticastAddress => iface.GetIPProperties().MulticastAddresses
                .Where(x => IsValidIPAddress(x.Address))
                .Select(x => x.Address)
                .FirstOrDefault();

            public string? IP => iface.GetIPProperties().UnicastAddresses
                .Where(x => IsValidIPAddress(x.Address))
                .Select(x => x.Address.ToString())
                .FirstOrDefault();

            public override string ToString()
            {
                return $"{iface.Name} ({IP ?? "Unknown address"}, bc addr {MulticastAddress})";
            }
        }

        static bool IsValidIPAddress(IPAddress ipAddress)
        {
            return ipAddress.AddressFamily == AddressFamily.InterNetwork;
        }

        private void LANMode_Load(object sender, EventArgs e)
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.Supports(NetworkInterfaceComponent.IPv4))
                .Where(x => x.GetIPProperties().MulticastAddresses.Any(y => IsValidIPAddress(y.Address)))
                .Where(x => x.GetIPProperties().UnicastAddresses.Any(y => IsValidIPAddress(y.Address)))
                .Select(x => new Interface(x))
                .ToList();

            lbInterfaces.DataSource = interfaces;

            var firstConfiguredPublicIP = wvsServers.Select(x => x.PublicIP).FirstOrDefault();

            if (firstConfiguredPublicIP != null)
            {
                // Try to find an interface with this IP
                var interfaceMatchingPublicIP = interfaces.Where(x =>
                {
                    var ip = x.IP;
                    if (ip == null) return false;
                    return ip == firstConfiguredPublicIP;
                }).FirstOrDefault();

                if (interfaceMatchingPublicIP != null)
                {
                    lbInterfaces.SelectedItem = interfaceMatchingPublicIP;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var ip = SelectedInterface.IP;
            if (ip == null)
            {
                MessageBox.Show("For some reason, no network IP was available.");
                DialogResult = DialogResult.None;
                return;
            }

            foreach (var server in wvsServers)
            {
                server.PublicIP = ip;
            }
        }
    }
}