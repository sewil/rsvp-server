using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WvsBeta.Launcher.Config
{
    public interface IConfig : INotifyPropertyChanged
    {
        void Reload();

        void Write();
    }
}
