using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using ChatPlayground.Views;
using ChatPlayground.ViewModels;
using ChatPlayground.Services;
using UraniumUI;
using ChatPlayground.Data;
using Plainer.Maui;
using ChatPlayground.Views.Popups;
using SimpleToolkit.SimpleShell;
using ChatPlayground.Handlers;
using MetroLog.MicrosoftExtensions;
using MetroLog.Operators;
using MudBlazor.Services;
using ChatPlayground.Blazor.Services;
using Majorsoft.Blazor.Components.Common.JsInterop;

namespace ChatPlayground
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()

                .UseMauiCommunityToolkit()
                .UseSimpleShell()
                .UseUraniumUI()
                .UseUraniumUIMaterial()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Roboto
                    fonts.AddFont("Roboto-Thin.ttf", "RobotoThin");
                    fonts.AddFont("Roboto-Light.ttf", "RobotoLight");
                    fonts.AddFont("Roboto-Regular.ttf", "RobotoRegular");
                    fonts.AddFont("Roboto-Italic.ttf", "RobotoItalic");
                    fonts.AddFont("Roboto-Bold.ttf", "RobotoBold");


                    // Segoe
                    fonts.AddFont("Segoe UI.ttf", "Segoe");
                    fonts.AddFont("Segoe UI Semi Light.ttf", "SegoeSemiLight");
                    fonts.AddFont("Segoe UI Light.ttf", "SegoeLight");
                    fonts.AddFont("Segoe UI Bold.ttf", "SegoeBold");

                    fonts.AddMaterialIconFonts();
                })
                .ConfigureMauiHandlers(handlers =>
                {
                    handlers.AddCustomHandlers();
                    handlers.AddPlainer();
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();
            builder.Services.AddJsInteropExtensions();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif       
            
            MauiExceptions.UnhandledException += OnUnhandledException;

            builder.RegisterServices();
            builder.RegisterViewModels();
            builder.RegisterViews();
            builder.ConfigureLogger();

            return builder.Build();
        }

        private static void RegisterViewModels(this MauiAppBuilder builder)
        {
            builder.Services.AddSingleton<AppShellViewModel>();
            builder.Services.AddSingleton<ConversationListViewModel>();
            builder.Services.AddSingleton<ModelListViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();

            builder.Services.AddSingleton<ChatPageViewModel>();
            builder.Services.AddTransient<ModelsPageViewModel>();
        }

        private static void RegisterViews(this MauiAppBuilder builder)
        {
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<ModelsPage>();
            builder.Services.AddTransientPopup<InformationPopup, InformationPopupViewModel>();
            builder.Services.AddTransientPopup<UnsortedModelFilesPopup, UnsortedModelFilesPopupViewModel>();
        }

        private static void RegisterServices(this MauiAppBuilder builder)
        {
            builder.Services.AddSingleton<AppSettingsService>();
            builder.Services.AddSingleton<IChatPlaygroundDatabase, ChatPlaygroundDatabase>();
            builder.Services.AddSingleton<ILLMFileManager, LLMFileManager>();
            builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
            builder.Services.AddSingleton<IPreferences>(Preferences.Default);
            builder.Services.AddSingleton<LMKitService>();
            builder.Services.AddSingleton<LLMFileManager>();
            builder.Services.AddSingleton<HttpClient>();

            builder.Services.AddScoped<ScrollInfoService>();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                //_logger.LogError("Unhandled exception occurred: " + exception.Message);
            }
        }

        public static void ConfigureLogger(this MauiAppBuilder builder)
        {
            builder.Logging
            .AddTraceLogger(
                options =>
                {
                    options.MinLevel = LogLevel.Trace;
                    options.MaxLevel = LogLevel.Critical;
                }) // Will write to the Debug Output
            .AddInMemoryLogger(
                options =>
                {
                    options.MaxLines = 1024;
                    options.MinLevel = LogLevel.Debug;
                    options.MaxLevel = LogLevel.Critical;
                })
            .AddStreamingFileLogger(
                options =>
                {
                    options.RetainDays = 2;
                    options.FolderPath = Path.Combine(
                        FileSystem.CacheDirectory,
                        "MetroLogs");
                });

            var path = FileSystem.CacheDirectory;
            builder.Services.AddSingleton(LogOperatorRetriever.Instance);
        }
    }
}
