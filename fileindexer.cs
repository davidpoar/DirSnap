#:package Microsoft.Data.Sqlite@8.0.0

using System;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

string dbPath = args.Length > 0 ? args[0] : "files.db";
string rootPath = args.Length > 1 ? args[1] : "";

if (string.IsNullOrWhiteSpace(rootPath))
{
    Console.Write("Ruta a indexar: ");
    rootPath = Console.ReadLine() ?? "";
}

if (!Directory.Exists(rootPath))
{
    Console.WriteLine("Ruta no válida");
    return;
}

InitDatabase();

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// 🚀 PRAGMAS de rendimiento
using (var pragma = connection.CreateCommand())
{
    pragma.CommandText = @"
    PRAGMA journal_mode = WAL;
    PRAGMA synchronous = NORMAL;
    PRAGMA temp_store = MEMORY;
    ";
    pragma.ExecuteNonQuery();
}

// ✅ TRANSACCIÓN CORRECTA (reutilizable)
var transaction = connection.BeginTransaction();

using var cmd = connection.CreateCommand();
cmd.CommandText =
@"INSERT OR REPLACE INTO Files
(FileName, FullPath, FileSize, LastModified)
VALUES ($name,$path,$size,$mod)";

cmd.Transaction = transaction;

// parámetros reutilizables
var pName = cmd.CreateParameter();
pName.ParameterName = "$name";
cmd.Parameters.Add(pName);

var pPath = cmd.CreateParameter();
pPath.ParameterName = "$path";
cmd.Parameters.Add(pPath);

var pSize = cmd.CreateParameter();
pSize.ParameterName = "$size";
cmd.Parameters.Add(pSize);

var pMod = cmd.CreateParameter();
pMod.ParameterName = "$mod";
cmd.Parameters.Add(pMod);

int count = 0;

foreach (var file in SafeEnumerateFiles(rootPath))
{
    try
    {
        var info = new FileInfo(file);

        pName.Value = info.Name;
        pPath.Value = info.FullName;
        pSize.Value = info.Length;
        pMod.Value = info.LastWriteTimeUtc.Ticks;

        cmd.ExecuteNonQuery();

        count++;

        // 💥 commit por bloques (FIX CORRECTO)
        if (count % 5000 == 0)
        {
            transaction.Commit();

            transaction = connection.BeginTransaction();
            cmd.Transaction = transaction;

            Console.WriteLine($"Indexados: {count}");
        }
    }
    catch
    {
        // puedes loggear si quieres
    }
}

// commit final
transaction.Commit();

Console.WriteLine($"Finalizado. Total: {count}");

void InitDatabase()
{
    if (File.Exists(dbPath)) return;

    using var connection = new SqliteConnection($"Data Source={dbPath}");
    connection.Open();

    var cmd = connection.CreateCommand();

    cmd.CommandText =
    @"
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

IEnumerable<string> SafeEnumerateFiles(string root)
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
            foreach (var f in files)
                yield return f;

        try { dirs = Directory.GetDirectories(dir); } catch { }

        if (dirs != null)
            foreach (var d in dirs)
                stack.Push(d);
    }
}