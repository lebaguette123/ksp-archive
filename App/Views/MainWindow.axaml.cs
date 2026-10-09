using System;
using App.ViewModels;
using Avalonia.Controls;

namespace App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async void ScanButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm) return;
            var path = this.FindControl<TextBox>("PathInput")?.Text;
            if (!string.IsNullOrEmpty(path))
            {
                await vm.ScanDirectoryAsync(path);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Scan failed: {ex.Message}");
            if(DataContext is MainWindowViewModel vm)
            {
                vm.StatusText = $"ERROR: {ex.Message}";
            }
        }
    }
}