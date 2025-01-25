using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WvsBeta.Common;
using WzTools.FileSystem;
using WzTools.Helpers;
using WzTools.Objects;
using Exception = System.Exception;

namespace WvsBeta.Launcher
{
    public partial class EventEditor : Form
    {
        public string FilePath => Path.Join("..", "DataSvr", "Server", "EventDate.img");
        private BindingList<Event> events { get; } = new BindingList<Event>();

        public EventEditor()
        {
            InitializeComponent();
            dgvEvents.DataSource = events;
        }

        class Event : INotifyPropertyChanged
        {
            public string LimitedName { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        IEnumerable<Event> LoadEvents()
        {
            using var events = new FSFile(FilePath);

            foreach (var prop in events)
            {
                var startDate = prop.GetInt32("startDate");
                var endDate = prop.GetInt32("endDate");
                if (startDate == null || endDate == null)
                {
                    Console.WriteLine($"Missing startDate or endDate on event {prop.Name}");
                    continue;
                }

                yield return new Event()
                {
                    LimitedName = prop.Name,
                    StartDate = ((int)startDate).AsYYYYMMDDHHDateTime(),
                    EndDate = ((int)endDate).AsYYYYMMDDHHDateTime(),
                };
            }
        }

        void SaveEvents()
        {
            // Save it as ASCII
            var cfg = new ConfigReader(FilePath, false);

            foreach (var e in events)
            {
                var eventProp = cfg.Set(e.LimitedName);
                eventProp.Set("startDate", e.StartDate.ToYYYYMMDDHHDateTime().ToString());
                eventProp.Set("endDate", e.EndDate.ToYYYYMMDDHHDateTime().ToString());
            }

            cfg.Write();
        }

        private void EventEditor_Load(object sender, EventArgs e)
        {
            try
            {
                LoadEvents().ForEach(x => events.Add(x));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to load events: {ex}");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SaveEvents();
        }
    }
}
