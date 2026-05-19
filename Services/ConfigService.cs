using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OneFileBox_new.Models;

namespace OneFileBox_new.Services;

public class ConfigService
{
    public static ConfigService Instance { get; } = new();
    private static ConfigService? _instance;

    public AppConfig Config { get; private set; } = new();
    public string ConfigDir { get; }

    private readonly string _configPath;

    public ConfigService()
    {
        _instance = this;
        ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OneFileBox");

        Directory.CreateDirectory(ConfigDir);
        _configPath = Path.Combine(ConfigDir, "config.json");
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Config = config;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[ConfigService] Load failed: {ex.Message}");
        }
        Config = new AppConfig();
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[ConfigService] Save failed: {ex.Message}");
        }
    }

    public async Task<List<RepoConfig>> LoadRepositoriesAsync()
    {
        await LoadAsync();
        return Config.Repositories;
    }

    public async Task AddOrUpdateRepoAsync(RepoConfig repo)
    {
        var existing = Config.Repositories.FindIndex(r => r.Key == repo.Key);
        if (existing >= 0)
            Config.Repositories[existing] = repo;
        else
            Config.Repositories.Add(repo);

        await SaveAsync();
    }

    public async Task RemoveRepoAsync(string key)
    {
        Config.Repositories.RemoveAll(r => r.Key == key);
        await SaveAsync();
    }

    public async Task SetLastActiveRepoAsync(string? key)
    {
        Config.LastActiveRepoKey = key;
        await SaveAsync();
    }
}
