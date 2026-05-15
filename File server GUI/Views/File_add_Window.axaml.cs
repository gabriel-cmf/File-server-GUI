using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using File_server_GUI.data;
using File_server_GUI.ViewModels;
using File_test_server_gui.data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.Views
{
    public partial class File_add_Window : Window
    {
        public File_add_Window()
        {
            InitializeComponent();
        }

        public void Confirm(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        public void Cancel(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        public async void Load_file(object? sender, RoutedEventArgs e)
        {
            var fam = DataContext as File_add_Model ?? throw new ArgumentNullException(nameof(DataContext));

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;

            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open The folder the files is in",
                AllowMultiple = false
            });

            foreach (var file in files) 
            {
                string? path = file.TryGetLocalPath();
                if(path != null)
                {
                    fam.update_file_registry(path);

                }
            }
        }


        public async void Edit_req_balance(File_registry_info fri)
        {
            File_registry? fr = null;
            if (fri.owner.TryGetTarget(out fr))
            {
                Balance_edit_Model bem = new();
                bem.new_balance = fr.required_credit;
                bem.str_balance = Server_main.bytesize_to_str(fr.required_credit);
                Edit_balance_Window ebw = new();
                ebw.DataContext = bem;
                bool res = await ebw.ShowDialog<bool>(this);


                if (res)
                {
                    fr.required_credit = bem.new_balance;
                    fr.update_fr_info();
                }
            }

        }
    }
}
