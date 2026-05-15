using CommunityToolkit.Mvvm.ComponentModel;
using File_server_GUI.data;
using File_server_GUI.Views;
using File_test_server_gui.data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.ViewModels
{
    public partial class File_add_Model : ViewModelBase
    {

        [ObservableProperty]
        private string full_path;

        [ObservableProperty]
        private string filesize;

        [ObservableProperty]
        private float upload_multiplier;


        [ObservableProperty]
        private bool need_balance;


        [ObservableProperty]
        private long req_balance;

        [ObservableProperty]
        private bool need_ratio;

        [ObservableProperty]
        private float req_ratio;

        public File_registry fr;

        public File_add_Model() 
        { 
            full_path = string.Empty;
            fr = new File_registry();
            filesize = string.Empty;
            upload_multiplier = 1.0f;
            need_balance = false;
            req_balance = 0;
            need_ratio = false;
            req_ratio = 0;
        }

        public bool update_file_registry(string path)
        {
            bool res = fr.try_register_file(path);
            if (res) 
            {
                fr.required_credit = Req_balance;
                fr.required_ratio = Req_ratio;
                fr.ignore_credit = !Need_balance;
                fr.ignore_ratio = !Need_ratio;
                fr.update_fr_info();
                Filesize = Server_main.bytesize_to_str(fr.filesize);
                Full_path = path;
            }

            return res;
        }


       
    }
}
