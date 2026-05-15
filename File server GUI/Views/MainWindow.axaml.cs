using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using File_server_GUI.data;
using File_server_GUI.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace File_server_GUI.Views
{
    public partial class MainWindow : Window
    {
        public Server_main sm;
        //public string file_full_name;

        public MainWindow()
        {
            sm = null;
            InitializeComponent();
            //file_full_name = string.Empty;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (this.DataContext is MainWindowViewModel vm)
            {

                sm= vm.sm;
                vm.main_window = this;
                sm.main_window = this;
                server_main_init();
                
            }
        }

        public void server_main_init()
        {
            Debug.WriteLine("Starting server listener");
            Task sm_task = Task.Factory.StartNew(() => { sm.listen_for_clients(); });
        }

        public void add_fr_info(File_registry_info fri)
        {
            var mwvm = DataContext as MainWindowViewModel;
            if (mwvm == null) { return; }
            mwvm.file_reg_observables.Add(fri);
            mwvm.fri_backup.Add(fri);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            

            Debug.WriteLine("Closing server");
            sm.add_log_msg("Closing server");
            sm.save_clients();
            sm.save_files();
            sm.stop_server();
            base.OnClosing(e);
            
        }

        



        public void add_client_observable(Client_observable co)
        {
            var mwvm = DataContext as MainWindowViewModel;
            if(mwvm == null) 
            {
                Debug.WriteLine("ERROR: could not insert client observable");
                return;
            
            }
            mwvm.client_observables.Add(co);
        }

        public async void Add_file(object? sender, RoutedEventArgs e)
        {
            File_add_Model fam = new();
            File_add_Window dialog = new();

            dialog.DataContext = fam;

            bool res = await dialog.ShowDialog<bool>(this);

            if(res)
            {
                sm.add_file_registry(fam.fr);
                var mwvm = DataContext as MainWindowViewModel;
                mwvm.file_reg_observables.Add(fam.fr.fr_info);
                
            }
        }
    }
}