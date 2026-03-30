using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Microsoft.Data.Sqlite;
using Viewer.Helpers;
using Viewer.Models;

namespace Viewer;

public partial class MainWindow : Window
{
    private List<FileItem> _db1Items = new();
    private List<FileItem> _db2Items = new();

    // Store in-memory
    private List<FileItem> _db1AllItems = new();
    private List<FileItem> _db2AllItems = new();

    private bool _isSyncing = false;

    private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

    public MainWindow()
    {
        InitializeComponent();
        // Kick off FFmpeg initialization early (background download if needed)
        _ = VideoThumbnailHelper.EnsureFfmpegAsync();
    }



    private async void BtnLoadDb1_Click(object? sender, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DB 1",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("SQLite Database") { Patterns = new[] { "*.db", "*.sqlite", "*.sqlite3" } },
                FilePickerFileTypes.All
            }
        });
        
        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            TxtDb1Path.Text = $"Loading {path}...";
            BtnLoadDb1.IsEnabled = false;

            try
            {
                var newItems = await Task.Run(() => LoadDatabase(path));
                _db1AllItems = newItems;

                TxtDb1Path.Text = $"Cross-referencing {path}...";
                await Task.Run(() => CrossReference());

                TxtDb1Path.Text = path;
                UpdateStats();
                await ApplyFilter1Async();
                await ApplyFilter2Async(); // DB1 change affects DB2 matching as well
            }
            catch (Exception)
            {
                TxtDb1Path.Text = $"Failed to load {path}";
            }
            finally
            {
                BtnLoadDb1.IsEnabled = true;
            }
        }
    }

    private async void BtnLoadDb2_Click(object? sender, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DB 2",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("SQLite Database") { Patterns = new[] { "*.db", "*.sqlite", "*.sqlite3" } },
                FilePickerFileTypes.All
            }
        });
        
        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            TxtDb2Path.Text = $"Loading {path}...";
            BtnLoadDb2.IsEnabled = false;

            try
            {
                var newItems = await Task.Run(() => LoadDatabase(path));
                _db2AllItems = newItems;

                TxtDb2Path.Text = $"Cross-referencing {path}...";
                await Task.Run(() => CrossReference());

                TxtDb2Path.Text = path;
                UpdateStats();
                await ApplyFilter1Async(); // DB2 change affects DB1 matching as well
                await ApplyFilter2Async(); 
            }
            catch (Exception)
            {
                TxtDb2Path.Text = $"Failed to load {path}";
            }
            finally
            {
                BtnLoadDb2.IsEnabled = true;
            }
        }
    }

    private List<FileItem> LoadDatabase(string path)
    {
        var list = new List<FileItem>();
        try 
        {
            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, FileName, FullPath, FileSize, LastModified, Hash FROM Files";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new FileItem
                {
                    Id = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    FileName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    FullPath = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    FileSize = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                    LastModified = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    Hash = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }
        }
        catch (Exception) { /* Ignored */ }
        return list;
    }

    private void CrossReference()
    {
        if (_db1AllItems == null || _db2AllItems == null) return;

        var db2ByHashSize = _db2AllItems.Where(x => !string.IsNullOrEmpty(x.Hash))
                                        .GroupBy(x => $"{x.Hash}_{x.FileSize}")
                                        .ToDictionary(g => g.Key, g => g.First());
                                        
        var db2ByNameSize = _db2AllItems.GroupBy(x => $"{x.FileName}_{x.FileSize}")
                                        .ToDictionary(g => g.Key, g => g.First());
        
        var db1ByHashSize = _db1AllItems.Where(x => !string.IsNullOrEmpty(x.Hash))
                                        .GroupBy(x => $"{x.Hash}_{x.FileSize}")
                                        .ToDictionary(g => g.Key, g => g.First());
                                        
        var db1ByNameSize = _db1AllItems.GroupBy(x => $"{x.FileName}_{x.FileSize}")
                                        .ToDictionary(g => g.Key, g => g.First());

        foreach (var item in _db1AllItems)
        {
            bool matched = false;
            if (!string.IsNullOrEmpty(item.Hash) && db2ByHashSize.ContainsKey($"{item.Hash}_{item.FileSize}"))
                matched = true;
            else if (db2ByNameSize.ContainsKey($"{item.FileName}_{item.FileSize}"))
                matched = true;

            item.IsMatched = matched;
        }

        foreach (var item in _db2AllItems)
        {
            bool matched = false;
            if (!string.IsNullOrEmpty(item.Hash) && db1ByHashSize.ContainsKey($"{item.Hash}_{item.FileSize}"))
                matched = true;
            else if (db1ByNameSize.ContainsKey($"{item.FileName}_{item.FileSize}"))
                matched = true;

            item.IsMatched = matched;
        }
    }

    private void UpdateStats()
    {
        if (TxtStats1 == null || TxtStats2 == null) return;

        int db1Total = _db1AllItems.Count;
        int db1Matched = _db1AllItems.Count(x => x.IsMatched);
        double db1Pct = db1Total > 0 ? (double)db1Matched / db1Total * 100 : 0;
        TxtStats1.Text = $"{db1Total} items ({db1Pct:F1}% matched)";

        int db2Total = _db2AllItems.Count;
        int db2Matched = _db2AllItems.Count(x => x.IsMatched);
        double db2Pct = db2Total > 0 ? (double)db2Matched / db2Total * 100 : 0;
        TxtStats2.Text = $"{db2Total} items ({db2Pct:F1}% matched)";
    }

    private async Task ApplyFilter1Async()
    {
        if (Grid1 == null || CmbFilter1 == null) return;

        int index = CmbFilter1.SelectedIndex;
        
        var filteredLists = await Task.Run(() => 
        {
            var list = new List<FileItem>(_db1AllItems.Count);
            foreach (var item in _db1AllItems)
            {
                if (index == 0 || (index == 1 && item.IsMatched) || (index == 2 && !item.IsMatched))
                    list.Add(item);
            }
            return list;
        });

        _db1Items = filteredLists;
        Grid1.ItemsSource = null;
        Grid1.ItemsSource = _db1Items;
    }

    private async Task ApplyFilter2Async()
    {
        if (Grid2 == null || CmbFilter2 == null) return;

        int index = CmbFilter2.SelectedIndex;
        
        var filteredLists = await Task.Run(() => 
        {
            var list = new List<FileItem>(_db2AllItems.Count);
            foreach (var item in _db2AllItems)
            {
                if (index == 0 || (index == 1 && item.IsMatched) || (index == 2 && !item.IsMatched))
                    list.Add(item);
            }
            return list;
        });

        _db2Items = filteredLists;
        Grid2.ItemsSource = null;
        Grid2.ItemsSource = _db2Items;
    }

    private async void CmbFilter1_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await ApplyFilter1Async();
    }

    private async void CmbFilter2_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await ApplyFilter2Async();
    }

    private void DataGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is FileItem item)
        {
            if (item.IsMatched)
            {
                e.Row.Classes.Add("matched");
                e.Row.Classes.Remove("unmatched");
            }
            else
            {
                e.Row.Classes.Add("unmatched");
                e.Row.Classes.Remove("matched");
            }
        }
    }

    private void Grid1_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing) return;
        
        var selected = Grid1.SelectedItem as FileItem;
        LoadImagePreview(selected, ImgPreview1, TxtImgPath1);

        if (selected != null)
        {
            _isSyncing = true;
            var match = _db2Items.FirstOrDefault(x => 
                (!string.IsNullOrEmpty(selected.Hash) && x.Hash == selected.Hash && x.FileSize == selected.FileSize) ||
                (x.FileName == selected.FileName && x.FileSize == selected.FileSize));
            
            if (match != null)
            {
                Grid2.SelectedItem = match;
                Grid2.ScrollIntoView(match, null);
                LoadImagePreview(match, ImgPreview2, TxtImgPath2);
            }
            else
            {
                Grid2.SelectedItem = null;
                ImgPreview2.Source = null;
                if (TxtImgPath2 != null) TxtImgPath2.Text = string.Empty;
            }
            _isSyncing = false;
        }
        else
        {
            ImgPreview1.Source = null;
            if (TxtImgPath1 != null) TxtImgPath1.Text = string.Empty;
        }
    }

    private void Grid2_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing) return;

        var selected = Grid2.SelectedItem as FileItem;
        LoadImagePreview(selected, ImgPreview2, TxtImgPath2);

        if (selected != null)
        {
            _isSyncing = true;
            var match = _db1Items.FirstOrDefault(x => 
                (!string.IsNullOrEmpty(selected.Hash) && x.Hash == selected.Hash && x.FileSize == selected.FileSize) ||
                (x.FileName == selected.FileName && x.FileSize == selected.FileSize));
            
            if (match != null)
            {
                Grid1.SelectedItem = match;
                Grid1.ScrollIntoView(match, null);
                LoadImagePreview(match, ImgPreview1, TxtImgPath1);
            }
            else
            {
                Grid1.SelectedItem = null;
                ImgPreview1.Source = null;
                if (TxtImgPath1 != null) TxtImgPath1.Text = string.Empty;
            }
            _isSyncing = false;
        }
        else
        {
            ImgPreview2.Source = null;
            if (TxtImgPath2 != null) TxtImgPath2.Text = string.Empty;
        }
    }

    private void LoadImagePreview(FileItem? item, Image imgControl, TextBlock pathTextControl)
    {
        imgControl.Source = null;
        if (pathTextControl != null) pathTextControl.Text = string.Empty;

        if (item == null) return;

        if (pathTextControl != null)
            pathTextControl.Text = item.FullPath;

        if (!File.Exists(item.FullPath)) return;

        var ext = Path.GetExtension(item.FullPath);

        if (_imageExtensions.Contains(ext ?? ""))
        {
            try
            {
                Task.Run(() =>
                {
                    var bitmap = new Bitmap(item.FullPath);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => imgControl.Source = bitmap);
                });
            }
            catch { /* Handle invalid images gracefully */ }
        }
        else if (VideoThumbnailHelper.VideoExtensions.Contains(ext ?? ""))
        {
            var fullPath = item.FullPath; // capture for closure
            if (pathTextControl != null)
                pathTextControl.Text = fullPath + " ⏳ descargando FFmpeg (solo primera vez)...";

            Task.Run(async () =>
            {
                // Update to 'generando preview' once FFmpeg is ready
                var ffmpegError = await VideoThumbnailHelper.EnsureFfmpegAsync();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (pathTextControl != null)
                        pathTextControl.Text = ffmpegError != null
                            ? fullPath + $" ⚠️ {ffmpegError}"
                            : fullPath + " ⏳ generando preview...";
                });

                if (ffmpegError != null) return;

                var (thumbPath, thumbError) = await VideoThumbnailHelper.GetVideoThumbnailAsync(fullPath);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (thumbPath != null)
                    {
                        try { imgControl.Source = new Bitmap(thumbPath); }
                        catch { }
                    }
                    if (pathTextControl != null)
                        pathTextControl.Text = thumbError != null
                            ? fullPath + $" ⚠️ {thumbError}"
                            : fullPath;
                });
            });
        }
    }
}