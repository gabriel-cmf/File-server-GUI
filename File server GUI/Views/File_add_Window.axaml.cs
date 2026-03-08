using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using File_server_GUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;

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
    }
}
