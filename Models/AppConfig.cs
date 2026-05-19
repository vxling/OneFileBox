using System.Collections.Generic;

namespace OneFileBox_new.Models;

public class AppConfig
{
    public List<RepoConfig> Repositories { get; set; } = [];

    public string? LastActiveRepoKey { get; set; }
}
