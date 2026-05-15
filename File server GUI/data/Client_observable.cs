using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.data
{
    public partial class Client_observable : ObservableObject
    {
        [ObservableProperty]
        private string ip_adress;

        [ObservableProperty]
        private string flag;

        [ObservableProperty]
        private bool online;

        [ObservableProperty]
        private long balance;

        [ObservableProperty]
        private string formt_balance;

        [ObservableProperty]
        private double ratio;

        [ObservableProperty]
        private bool banned;

        public WeakReference<Client_registry> owner;

        public Client_observable(Client_registry cr)
        {
            ip_adress="0.0.0.0";
            flag = "🇺🇳";
            online = false;
            balance = 0;
            ratio = 0;
            formt_balance = String.Empty;
            owner = new WeakReference<Client_registry>(cr);
        }
    }
}
