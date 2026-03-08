using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using File_test_server_gui.data;

namespace File_server_GUI.data
{
    public partial class File_registry_info : ObservableObject
    {
        [ObservableProperty]
        private string full_name;

        [ObservableProperty]
        private string full_path;

        [ObservableProperty]
        private string filesize;

        [ObservableProperty]
        private float file_bonus;

        public WeakReference<File_registry> owner; 


        public File_registry_info(File_registry fr)
        {
            full_name = string.Empty;
            full_path = string.Empty;
            filesize = string.Empty;
            file_bonus = 0;
            owner = new WeakReference<File_registry>(fr);
        }
    }
}
