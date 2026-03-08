using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using File_server_GUI.ViewModels;

namespace File_server_GUI.Views
{
    public partial class Edit_balance_Window : Window
    {
        public Edit_balance_Window() 
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

        public void Reset_balance(object? sender, RoutedEventArgs e)
        {
            var bem = DataContext as Balance_edit_Model;
            if(bem==null) { return; }
            bem.Reset_balance();
        }

        public void Sub_100(object? sender, RoutedEventArgs e)
        {
            var bem = DataContext as Balance_edit_Model;
            if (bem == null) { return; }
            bem.change_balance(-100);
        }

        public void Sub_10(object? sender, RoutedEventArgs e)
        {
            var bem = DataContext as Balance_edit_Model;
            if (bem == null) { return; }
            bem.change_balance(-10);
        }

        public void Sub_1(object? sender, RoutedEventArgs e)
        {
            var bem = DataContext as Balance_edit_Model;
            if (bem == null) { return; }
            bem.change_balance(-1);
        }

        public void Add_100(object? sender, RoutedEventArgs e)
        {
            var bem = DataContext as Balance_edit_Model;
            if (bem == null) { return; }
            bem.change_balance(100);
        }

        public void Add_10(object? sender, RoutedEventArgs e)
        {
            var bem = DataContext as Balance_edit_Model;
            if (bem == null) { return; }
            bem.change_balance(10);
        }

        public void Add_1(object? sender, RoutedEventArgs e)
        {
            var bem = DataContext as Balance_edit_Model;
            if (bem == null) { return; }
            bem.change_balance(1);
        }

    }
}
