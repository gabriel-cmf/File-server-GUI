using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using File_server_GUI.ViewModels;
using File_test_server_gui.data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.Views
{
    public partial class File_edit_Window : Window
    {
        public string file_full_name;
        public File_registry? og_file;

        public File_edit_Window()
        {
            InitializeComponent();
            //file_full_name = string.Empty;
            og_file = null;

        }

        public void Confirm(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        public void Cancel(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        public void Reset_folder(object? sender, RoutedEventArgs e)
        {
            var Fa_model = DataContext as File_edit_Model ?? throw new ArgumentNullException(nameof(DataContext));
            if (Fa_model == null) { return; }
            Fa_model.Folder_path = Fa_model.Og_folder_path;
        }



        public async void Load_folder(object? sender, RoutedEventArgs e)
        {

            var Fa_model = DataContext as File_edit_Model ?? throw new ArgumentNullException(nameof(DataContext));
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) 
            { 
                return; 
            
            }
            bool file_found = false;
            //if (og_file == null) { return; }

            if (Fa_model.Full_file_name == string.Empty)
            {
                throw new ArgumentNullException(nameof(Fa_model.Full_file_name));
            }




            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open The folder the files is in",
                AllowMultiple = false
            });

            

            if (folders != null && folders.Any())
            {
                Debug.WriteLine("File name:");
                Debug.WriteLine(Fa_model.Full_file_name);
                foreach (var folder in folders)
                {

                    string? f_path = folder.TryGetLocalPath();
                    if(f_path == null)
                    {
                        return;
                    }
                    try
                    {
                        var file = await folder.GetFileAsync(Fa_model.Full_file_name);
                        file_found = true;

                        string complete_path = f_path + "\\" + Fa_model.Full_file_name;
                        Debug.WriteLine(complete_path);
                        string file_hash = Server_main.generate_incremental_hash(complete_path);

                        if (file_hash == Fa_model.File_hash)
                        {
                            Fa_model.Folder_path = f_path;
                            Fa_model.Status = "File found in new location";

                        }
                        else 
                        {
                            Fa_model.Status = "Error: File found in folder, but hash does not match";
                        }



                    }
                    catch
                    {
                        Fa_model.Status = "Error: File not found in folder, please check if the name matches";
                        file_found = false;
                        break;
                    }
                }
            }
            else
            {
                Fa_model.Status = "Error: No folder selected or found";
            }

            


        }
    }
}

