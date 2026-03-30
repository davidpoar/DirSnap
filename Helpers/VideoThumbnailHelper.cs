using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xabe.FFmpeg.Downloader;

namespace Viewer.Helpers;

public static class VideoThumbnailHelper
{
    private static readonly string _ffmpegDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ViewerApp", "ffmpeg");
    private static readonly string _thumbnailCacheDir =
        Path.Combine(Path.GetTempPath(), "ViewerThumbnails");
    private static Task<string?>? _ffmpegEnsureTask;
    private static readonly object _ffmpegLock = new();

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".mpg", ".mpeg" };

    // Ensures FFmpeg binaries are present, downloading them on first launch
    // Returns null on success, or an error message string on failure
    public static Task<string?> EnsureFfmpegAsync()
    {
        lock (_ffmpegLock)
        {
            if (_ffmpegEnsureTask == null)
            {
                _ffmpegEnsureTask = Task.Run<string?>(async () =>
                {
                    try
                    {
                        Directory.CreateDirectory(_ffmpegDir);
                        var execName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
                        if (!File.Exists(Path.Combine(_ffmpegDir, execName)))
                            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _ffmpegDir);

                        // Verify binary was actually placed
                        if (!File.Exists(Path.Combine(_ffmpegDir, execName)))
                            return "FFmpeg no descargado";

                        return null; // success
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                });
            }
            return (Task<string?>)_ffmpegEnsureTask;
        }
    }

    // Extracts a thumbnail from a video file, caching it by MD5 of the path.
    // Returns null + sets errorMsg on failure.
    public static async Task<(string? thumbPath, string? error)> GetVideoThumbnailAsync(string videoPath)
    {
        try
        {
            Directory.CreateDirectory(_thumbnailCacheDir);
            var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(videoPath)));
            var thumbPath = Path.Combine(_thumbnailCacheDir, $"{hash}.jpg");

            if (File.Exists(thumbPath))
                return (thumbPath, null);

            var ffmpegError = await EnsureFfmpegAsync();
            if (ffmpegError != null)
                return (null, $"FFmpeg: {ffmpegError}");

            var execName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            var ffmpegExe = Path.Combine(_ffmpegDir, execName);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegExe,
                // No -ss: grab the very first keyframe (avoids issues with short videos)
                Arguments = $"-i \"{videoPath}\" -vframes 1 -q:v 2 \"{thumbPath}\" -y",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,  // must redirect to prevent buffer deadlock
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return (null, "No se pudo iniciar FFmpeg");

            // IMPORTANT: read stderr/stdout concurrently BEFORE awaiting exit.
            // FFmpeg always writes media info to stderr; if we don't drain it,
            // the pipe buffer fills up and FFmpeg blocks → deadlock.
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync();
            await Task.WhenAll(stderrTask, stdoutTask); // drain both streams

            if (!File.Exists(thumbPath))
                return (null, $"FFmpeg exit {process.ExitCode}");

            return (thumbPath, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
