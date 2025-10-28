using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModSyncTool.Models;

namespace ModSyncTool.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _localConfigPath;
    private readonly string _publisherProfilePath;
    private readonly string _skipFlagPath;

    public ConfigService()
    {
        var root = AppContext.BaseDirectory;
        _localConfigPath = Path.Combine(root, "local_config.json");
        _publisherProfilePath = Path.Combine(root, "publisher_profile.json");
        _skipFlagPath = Path.Combine(root, "skip_config_check.flag");
    }

    public string LocalConfigPath => _localConfigPath;

    public string SkipFlagPath => _skipFlagPath;

    public Task<bool> LocalConfigExistsAsync()
    {
        return Task.FromResult(File.Exists(_localConfigPath));
    }

    public async Task<LocalConfig?> LoadLocalConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_localConfigPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_localConfigPath);
        return await JsonSerializer.DeserializeAsync<LocalConfig>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveLocalConfigAsync(LocalConfig config, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_localConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> SkipFlagExistsAsync()
    {
        return Task.FromResult(File.Exists(_skipFlagPath));
    }

    public async Task CreateSkipFlagAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_skipFlagPath);
        await stream.WriteAsync(Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
    }

    public void DeleteSkipFlag()
    {
        if (File.Exists(_skipFlagPath))
        {
            File.Delete(_skipFlagPath);
        }
    }

    public async Task<PublisherProfile?> LoadPublisherProfileAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_publisherProfilePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_publisherProfilePath);
        return await JsonSerializer.DeserializeAsync<PublisherProfile>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePublisherProfileAsync(PublisherProfile profile, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_publisherProfilePath);
        await JsonSerializer.SerializeAsync(stream, profile, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
