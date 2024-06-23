using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Wpf;


namespace WebView2TestApp;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var app = new Application();

        // Create a window
        var window = new Window
        {
            Title = "WPF Window in Console Application",
            Width = 800,
            Height = 600,
            Content = new TextBlock { Text = "Hello, WPF!", TextAlignment = TextAlignment.Center }
        };

        var grid = new Grid();
        window.Content = grid;

        // Add a TextBlock to the Grid
        var textBlock = new TextBlock { Text = "Hello, WPF!", TextAlignment = TextAlignment.Center };
        grid.Children.Add(textBlock);

        // Create and initialize the WebView2 control
        var webView = new WebView2()
        {
            Name = "webView",
            Source = new Uri("https://www.bing.com")
        };
        grid.Children.Add(webView);
        webView.Loaded += (sender, e) =>
        {
        };

        //InitializeAsync(webView, grid);
        // Set the window as the application's main window and run the application
        app.Run(window);
    }

    static async void InitializeAsync(WebView2 webView, Grid grid)
    {
        // Ensure the CoreWebView2 environment is initialized
        await webView.EnsureCoreWebView2Async(null).ConfigureAwait(false);

        // Once initialized, add the WebView2 control to the Grid
        grid.Children.Add(webView);

        // Navigate to a website
        webView.Source = new Uri("https://www.baidu.com");
    }
    //static async Task Main2(string[] args)
    //{
    //    var options = WindowOptions.Default with
    //    {
    //        API = GraphicsAPI.None
    //    };
    //    options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
    //    options.Title = "WebView2 with Silk.NET";

    //    var window = Window.Create(options);
    //    Console.WriteLine("Hello, World!");

    //    // Ensure the window is created before proceeding

    //    window.Load += async () =>
    //    {
    //        // Obtain the HWND from the Silk.NET window
    //        var hwnd = (window.Native.Win32.Value).Hwnd;

    //        var thread = new Thread(async () =>
    //        {
    //            // Initialize WebView2
    //            var env = await CoreWebView2Environment.CreateAsync(null, null, null);
    //            var webView2 = await env.CreateCoreWebView2ControllerAsync(hwnd);

    //            // Set bounds and initial settings if necessary
    //            webView2.Bounds = new System.Drawing.Rectangle(0, 0, 800, 600);
    //            webView2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

    //            webView2.CoreWebView2.NavigationStarting += (sender, args) =>
    //            {
    //                Console.WriteLine("Navigation Starting");
    //            };

    //            webView2.CoreWebView2.NewWindowRequested += (sender, args) =>
    //            {
    //                Console.WriteLine("New Window Requested");
    //            };

    //            webView2.CoreWebView2.Navigate("https://www.baidu.com");
    //        });

    //        thread.SetApartmentState(ApartmentState.STA);
    //        thread.Start();



    //    };


    //    window.Run();
    //}
}
