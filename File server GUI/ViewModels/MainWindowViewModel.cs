using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using File_server_GUI.data;
using File_server_GUI.Views;
using File_test_server_gui.data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Tmds.DBus.Protocol;

namespace File_server_GUI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public string Greeting { get; } = "Welcome to Avalonia!";
        public Server_main sm;
        public ObservableCollection<Client_observable> client_observables { get; }
        public ObservableCollection<File_registry_info> file_reg_observables { get; }
        public List<File_registry_info> fri_backup;
        public MainWindow main_window;

        [ObservableProperty]
        private string filter_str;

        public MainWindowViewModel(Server_main s)
        {
            Debug.WriteLine("Creating MainWindowViewModel");
            sm = s ?? throw new ArgumentNullException(nameof(s));
            client_observables = new();
            file_reg_observables = new();
            fri_backup= new();
            main_window=null;
            filter_str = string.Empty;
        }

        partial void OnFilter_strChanged(string value)
        {
            filter_datagrid(value);
        }

        public void filter_datagrid(string query)
        {
            file_reg_observables.Clear();

            if(String.IsNullOrWhiteSpace(query))
            {
                foreach(File_registry_info fri in fri_backup)
                {
                    file_reg_observables.Add(fri);
                    
                }
                return;
            }

            var res = fri_backup.Where(x => x.Full_name.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach(File_registry_info fri in res)
            {
                file_reg_observables.Add(fri);
            }
        }

        [RelayCommand]
        public async void File_registry_edit(File_registry_info fri)
        {

            File_registry fr;
            if(!fri.owner.TryGetTarget(out fr))
            {
                throw new ArgumentNullException(nameof(fri));
                return;
            }

            var er = fr ?? throw new ArgumentNullException(nameof(fr));
            var fam = new File_edit_Model(fr) ;
            /*bool changed = Dispatcher.UIThread.InvokeAsync(async () => {

            
               

            }).Result;*/

            

            var dialog = new File_edit_Window();
            dialog.DataContext = fam;


            bool res = await dialog.ShowDialog<bool>(main_window);

            bool changed = res;

            if (changed) 
            {
                //fr.full_path = fam.Folder_path;
                fr.change_full_path(fam.Folder_path);
                
                fr.update_fr_info();
            }
        }


        [RelayCommand]
        public async void Export_download_file(File_registry_info fri)
        {

            File_registry fr;

            bool res = fri.owner.TryGetTarget(out fr);
            if (!res) { return; }

            var topLevel = TopLevel.GetTopLevel(main_window);
            if (topLevel == null)
            {
                return;

            }

            var df_name = fr.file_name;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export file",
                SuggestedFileName = df_name + ".json"
                
            });

            if (file != null) 
            {
                Dictionary<string, object> data = new Dictionary<string, object>();
                data["hash"] = fr.file_hash;
                data["url"] = sm.get_main_ip();
                data["port"] = sm.get_main_port();
                string json_dict = JsonConvert.SerializeObject(data);
                await using var stream = await file.OpenWriteAsync();
                using var writer = new StreamWriter(stream);
                await writer.WriteLineAsync(json_dict);
            }


            




        }


        [RelayCommand]
        public async void Edit_client_balance(Client_observable cob)
        {
              Client_registry? cr  = null;
              if(cob.owner.TryGetTarget(out cr))
              {
                   Balance_edit_Model bem = new();
                   bem.new_balance = cr.balance;
                   Edit_balance_Window ebw = new();
                   ebw.DataContext = bem;
                   bool res = await ebw.ShowDialog<bool>(main_window);


                   if(res)
                   {
                          cr.balance = bem.new_balance;
                          cr.update_observable();
                   }
            }

        }


    }
}
