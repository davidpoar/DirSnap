using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;
using Viewer.Helpers;

namespace Viewer;

// Return value class to pass the chosen destination back to main window
public class IndexerWizardResult
{
    public string DbPath { get; set; } = string.Empty;
    public int LoadIntoPanel { get; set; } = 0; // 0 = none, 1 = DB1, 2 = DB2
}

public partial class IndexerWizardWindow : Window
{
    private string _sourcePath = string.Empty;
    private string _destPath = string.Empty;

    public IndexerWizardResult Result { get; } = new();

    public IndexerWizardWindow()
    {
        InitializeComponent();
    }

    private async void BtnSelectSource_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Selecciona la carpeta raíz a indexar",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            _sourcePath = folders[0].Path.LocalPath;
            TxtSourceFolder.Text = _sourcePath;
            CheckReady();
        }
    }

    private async void BtnSelectDest_Click(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar base de datos como...",
            DefaultExtension = "db",
            FileTypeChoices = new[] { new FilePickerFileType("SQLite Database") { Patterns = new[] { "*.db" } } }
        });

        if (file != null)
        {
            _destPath = file.Path.LocalPath;
            TxtDbDest.Text = _destPath;
            CheckReady();
        }
    }

    private void CheckReady()
    {
        BtnStart.IsEnabled = !string.IsNullOrWhiteSpace(_sourcePath) && !string.IsNullOrWhiteSpace(_destPath);
    }

    private async void BtnStart_Click(object? sender, RoutedEventArgs e)
    {
        PanelConfig.IsEnabled = false;
        PanelButtons.IsVisible = false;
        PanelProgress.IsVisible = true;

        var progress = new Progress<string>(msg =>
        {
            TxtProgress.Text = msg;
        });

        try
        {
            await FileIndexerHelper.IndexDirectoryAsync(_sourcePath, _destPath, progress);

            // Hide progress bar, show completion
            PrgStatus.IsVisible = false;
            PanelCompletion.IsVisible = true;
            Result.DbPath = _destPath;
        }
        catch (Exception ex)
        {
            TxtProgress.Text = "Error: " + ex.Message;
            PanelButtons.IsVisible = true;
            BtnStart.IsEnabled = true;
            PanelConfig.IsEnabled = true;
            PrgStatus.IsVisible = false;
        }
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(Result);
    }

    private void BtnLoadDb1_Click(object? sender, RoutedEventArgs e)
    {
        Result.LoadIntoPanel = 1;
        Close(Result);
    }

    private void BtnLoadDb2_Click(object? sender, RoutedEventArgs e)
    {
        Result.LoadIntoPanel = 2;
        Close(Result);
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Result.LoadIntoPanel = 0;
        Close(Result);
    }
}
