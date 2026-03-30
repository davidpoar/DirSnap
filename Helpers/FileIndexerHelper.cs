using System;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Viewer.Helpers;

public static class FileIndexerHelper
{
    public static async Task IndexDirectoryAsync(string rootPath, string dbPath, bool calculateHash, IProgress<string> progress)
    {
        await Task.Run(() => 
        {
            InitDatabase(dbPath);

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA temp_store = MEMORY;
                ";
                pragma.ExecuteNonQuery();
            }

            var transaction = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Files (FileName, FullPath, FileSize, LastModified, Hash) VALUES ($name,$path,$size,$mod,$hash)";
            cmd.Transaction = transaction;

            var pName = cmd.CreateParameter(); pName.ParameterName = "$name"; cmd.Parameters.Add(pName);
            var pPath = cmd.CreateParameter(); pPath.ParameterName = "$path"; cmd.Parameters.Add(pPath);
            var pSize = cmd.CreateParameter(); pSize.ParameterName = "$size"; cmd.Parameters.Add(pSize);
            var pMod = cmd.CreateParameter();  pMod.ParameterName = "$mod";  cmd.Parameters.Add(pMod);
            var pHash = cmd.CreateParameter(); pHash.ParameterName = "$hash"; cmd.Parameters.Add(pHash);

            int count = 0;
            progress?.Report("Iniciando escaneo...");

            foreach (var file in SafeEnumerateFiles(rootPath))
            {
                try
                {
                    var info = new FileInfo(file);
                    pName.Value = info.Name;
                    pPath.Value = info.FullName;
                    pSize.Value = info.Length;
                    pMod.Value = info.LastWriteTimeUtc.Ticks;

                    if (calculateHash)
                    {
                        using var stream = File.OpenRead(file);
                        using var sha256 = SHA256.Create();
                        var hashBytes = sha256.ComputeHash(stream);
                        pHash.Value = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                    else
                    {
                        pHash.Value = DBNull.Value;
                    }

                    cmd.ExecuteNonQuery();
                    count++;

                    if (count % 1000 == 0)
                    {
                        transaction.Commit();
                        transaction = connection.BeginTransaction();
                        cmd.Transaction = transaction;

                        progress?.Report($"Indexados: {count} archivos...");
                    }
                }
                catch { }
            }

            transaction.Commit();
            progress?.Report($"Finalizado. Total indexados: {count} archivos.");
        });
    }

    private static void InitDatabase(string dbPath)
    {
        if (File.Exists(dbPath)) return;
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE Files (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FileName TEXT,
            FullPath TEXT UNIQUE,
            FileSize INTEGER,
            LastModified INTEGER,
            Hash TEXT
        );
        CREATE INDEX idx_name ON Files(FileName);
        CREATE INDEX idx_size ON Files(FileSize);
        CREATE INDEX idx_hash ON Files(Hash);
        ";
        cmd.ExecuteNonQuery();
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[]? files = null;
            string[]? dirs = null;

            try { files = Directory.GetFiles(dir); } catch { }
            if (files != null)
                foreach (var f in files) yield return f;

            try { dirs = Directory.GetDirectories(dir); } catch { }
            if (dirs != null)
                foreach (var d in dirs) stack.Push(d);
        }
    }
}
