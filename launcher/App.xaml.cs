using System;
using System.Windows;

namespace ZoidGundamLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            int i = Array.IndexOf(e.Args, "--screenshot-publish");
            if (i >= 0 && i + 1 < e.Args.Length)
            {
                new PublishWindow(e.Args[i + 1]).Show();
                return;
            }
            new MainWindow().Show();
        }
    }
}
