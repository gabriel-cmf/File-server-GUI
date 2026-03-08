using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using File_test_server_gui.data;

namespace File_server_GUI.ViewModels
{
    public partial class File_add_Model : ViewModelBase
    {

        [ObservableProperty]
        private string full_path;

        [ObservableProperty]
        private string filesize;

        public File_registry fr;

        public File_add_Model() 
        { 
            full_path = string.Empty;
            fr = new File_registry();
            filesize = string.Empty;
        }

        public bool update_file_registry(string path)
        {
            bool res = fr.try_register_file(path);
            if (res) 
            {
                fr.update_fr_info();
                Filesize = Server_main.bytesize_to_str(fr.filesize);
            }

            return res;
        }
    }
}
