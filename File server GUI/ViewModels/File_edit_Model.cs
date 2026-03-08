using CommunityToolkit.Mvvm.ComponentModel;
using File_server_GUI.data;
using File_test_server_gui.data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace File_server_GUI.ViewModels
{
    public partial class File_edit_Model : ViewModelBase
    {

        [ObservableProperty]
        private string folder_path;

        [ObservableProperty]
        private string og_folder_path;

        [ObservableProperty]
        private string full_file_name;

        [ObservableProperty]
        private string filesize;

        [ObservableProperty]
        private string file_hash;

        [ObservableProperty]
        private string status;

        [ObservableProperty]
        private File_registry og_file_registry;

        [ObservableProperty]
        private File_registry? gen_file_registry;

        public File_edit_Model()
        {
            folder_path = string.Empty;
            gen_file_registry = null;
            full_file_name = string.Empty;
            filesize = string.Empty;
            file_hash = string.Empty;
            og_folder_path = String.Empty;
        }

        public File_edit_Model(File_registry og_fr)
        {
            og_file_registry = og_fr;
            folder_path = og_folder_path = og_file_registry.path;
            gen_file_registry = null;
            full_file_name = og_file_registry.file_name_ext;
            filesize = Server_main.bytesize_to_str(og_file_registry.filesize);
            file_hash = og_file_registry.file_hash;
            
        }

        
    }
}
