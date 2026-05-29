#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using OneFileBox.Models;

namespace OneFileBox.Services;

public class AppPaths
{
    public static string Base => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OneFileBox");

    public static string Config => Path.Combine(Base, "config");
    public static string WorkCopies => Path.Combine(Base, "workcopies");

    public static string ConfigFile => Path.Combine(Config, "config.json");
}

public class ConfigService
{
    private static ConfigService? _instance;
    public static ConfigService Instance => _instance ??= new ConfigService();

    private AppConfig _config = new();
    private readonly string _configPath = AppPaths.ConfigFile;

    public AppConfig Config => _config;

    public event Action? ConfigChanged;

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }

            // Apply timeouts from config → SvnCliService
            SvnCliService.FileTransferTimeoutMs = _config.FileTransferTimeoutSeconds * 1000;
            SvnCliService.LocalCommandTimeoutMs = _config.LocalCommandTimeoutSeconds * 1000;
        }
        catch (Exception ex)
        {
            SvnCliLog.Warning("ConfigService LoadAsync failed: {0}", ex.Message);
            _config = new AppConfig();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Config);
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            SvnCliLog.Warning("ConfigService SaveAsync failed: {0}", ex.Message);
        }
    }

    public void AddRepository(Models.Repository repo)
    {
        _config.Repositories.Add(repo);
        ConfigChanged?.Invoke();
    }

    public void RemoveRepository(string name)
    {
        _config.Repositories.RemoveAll(r => r.Name == name);
        ConfigChanged?.Invoke();
    }
}