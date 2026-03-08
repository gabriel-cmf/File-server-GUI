using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.ViewModels
{
    public partial class Balance_edit_Model : ViewModelBase
    {
        public long new_balance;
        public Dictionary<string, long> mag_dict;

        [ObservableProperty]
        public List<string> mag_strs;

        [ObservableProperty]
        public string str_balance;

        [ObservableProperty]
        public string combo_sel;


        [ObservableProperty]
        public string status;


        public Balance_edit_Model()
        {
            new_balance = 0;
            mag_strs = new List<string>();
            mag_strs.Add("B");
            mag_strs.Add("KB");
            mag_strs.Add("MB");
            mag_strs.Add("GB");
            mag_strs.Add("TB");

            str_balance = "0";

            status = "Value is OK";

            mag_dict = new Dictionary<string, long>();
            mag_dict["B"] = 1;
            mag_dict["KB"] = 1024;
            mag_dict["MB"] = (long)1024 *1024;
            mag_dict["GB"] = (long)1024 * 1024 * 1024;
            mag_dict["TB"] = (long)1024 * 1024 * 1024 * 1024;

            combo_sel = "B";
            
        }


        public void Reset_balance()
        {
            new_balance = 0;
            update_balance_str();
        }

        public void change_balance(long val)
        {
            Debug.WriteLine(Combo_sel);
            new_balance += val*mag_dict[Combo_sel];
            update_balance_str();
        }

        public void update_balance_str()
        {
            Str_balance = Server_main.bytesize_to_str(new_balance);
        }
    }
}
