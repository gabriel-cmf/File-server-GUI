using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using File_server_GUI.ViewModels;
using File_server_GUI.Views;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net;

/*BsonMapper.Global.RegisterType<IPEndPoint>(
    
    serialize: (endpoint) => endpoint.ToString(),

    deserialize: (bson) => IPEndPoint.Parse(bson.AsString)
);*/

namespace File_server_GUI
{
    public partial class App : Application
    {
        public IServiceProvider? services_prov { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {

            var services = new ServiceCollection();
            services.AddSingleton<Server_main>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();
            services_prov = services.BuildServiceProvider();

            var vm = services_prov.GetRequiredService<MainWindowViewModel>();


            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = vm,
                };

                var mw = desktop.MainWindow as MainWindow;
                mw.sm.init_file_registry_info();
                mw.sm.init_client_observables();
            }
           

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}