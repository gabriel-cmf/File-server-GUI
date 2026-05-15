using Avalonia.Logging;
using File_server_GUI.data;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace File_test_server_gui.data
{
    public class File_registry
    {
        [BsonId]
        public string file_hash { get; set; }

        public string file_name { get; set; }
        public string file_name_ext { get; set; }
        public string full_path { get; set; }
        public string path { get; set; }

        public string type { get; set; }
        public long filesize { get; set; }
        public float upload_bonus;
        public long required_credit;
        public float required_ratio;

        public bool ignore_credit;
        public bool ignore_ratio;

        [BsonIgnore]
        public File_registry_info fr_info;

        public File_registry()
        {
            filesize = 0;
            upload_bonus = 1.0f;
            file_hash = file_name = full_path = path = type = "";
            fr_info = new(this);
            required_credit = 0;
            required_ratio = 0;
            ignore_credit = ignore_ratio = true;

        }


        public bool try_register_file(string comp_path)
        {
            if (File.Exists(comp_path))
            {
                full_path = comp_path;
                gen_file_hash();
                return true;
            }
            else
            {
                return false;
            }

        }


        public void update_fr_info()
        {
            fr_info.Full_name = file_name_ext;
            fr_info.Full_path = full_path;
            fr_info.Filesize = Server_main.bytesize_to_str(filesize);
            fr_info.File_bonus = upload_bonus;
        }

        void gen_file_hash()
        {
            FileInfo file_info = new FileInfo(full_path);
            type = file_info.Extension;
            file_name = Path.GetFileNameWithoutExtension(full_path);
            file_name_ext = file_name + type;
            filesize = file_info.Length;
            if (file_info.DirectoryName != null) { path = file_info.DirectoryName; }



            using (FileStream fs = new FileStream(full_path, FileMode.Open, FileAccess.Read))
            {
                byte[] h_bytes = SHA256.HashData(fs);

                file_hash = Convert.ToHexString(h_bytes);
            }
        }

        public void change_full_path(string folder)
        {
            full_path = folder;
        }

        public bool is_valid()
        {
            bool valid =false ;

            try
            {
                using (FileStream fs = new FileStream(full_path, FileMode.Open, FileAccess.Read))
                {

                }
                valid = true;
            }
            catch (Exception e) 
            { 
               valid = false;
            }

            return valid;
        }

       
    }
}
