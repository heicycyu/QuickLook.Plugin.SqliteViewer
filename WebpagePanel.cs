using Microsoft.Web.WebView2.Core;
using QuickLook.Common.Helpers;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.SqliteViewer
{
    internal class WebpagePanel : UserControl
    {
        private Microsoft.Web.WebView2.Wpf.WebView2 _webView;

        private object _objectForScripting;

        public object ObjectForScripting
        {
            get => _objectForScripting;
            set
            {
                _objectForScripting = value;

                Dispatcher.Invoke(async () =>
                {
                    await _webView.EnsureCoreWebView2Async();

                    // Use `chrome.webview.hostObjects` instead of `window`
                    _webView.CoreWebView2.AddHostObjectToScript("external", value);
                });
            }
        }

        public WebpagePanel()
        {
            if (!Helper.IsWebView2Available())
            {
                Content = CreateDownloadButton();
            }
            else
            {
                _webView = new Microsoft.Web.WebView2.Wpf.WebView2
                {
                    CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
                    {
                        UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\\"),
                    },
                    DefaultBackgroundColor = OSThemeHelper.AppsUseDarkTheme() ? Color.FromArgb(255, 32, 32, 32) : Color.White, // Prevent white flash in dark mode
                };
                Content = _webView;
            }
        }

        public void Navigate(Uri uri)
        {
            if (_webView == null)
                return;

            _webView.Source = uri;
        }

        private object CreateDownloadButton()
        {
            var button = new Button
            {
                Content = "Viewing this file requires Microsoft Edge WebView2 to be installed. Click here to download it.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(20, 6, 20, 6)
            };
            button.Click += (sender, e) => Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");

            return button;
        }
    }
}

file static class Helper
{
    public static bool IsWebView2Available()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (Exception)
        {
            return false;
        }
    }
}
