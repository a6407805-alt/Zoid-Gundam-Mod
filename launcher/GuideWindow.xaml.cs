using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ZoidGundamLauncher
{
    public partial class GuideWindow : Window
    {
        public GuideWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenRepo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/a6407805-alt/Zoid-Gundam-Mod") { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
