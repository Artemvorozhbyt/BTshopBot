
using System.Text.Json;

public sealed class BlockedUsersService
{
    private readonly string _filePath;
    private readonly HashSet<long> _blockedUsers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BlockedUsersService(string filePath)
    {
        _filePath = filePath;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var users = JsonSerializer.Deserialize<List<long>>(json);

            if (users is not null)
                _blockedUsers.UnionWith(users);
        }
    }

    public async Task<bool> IsBlockedAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            return _blockedUsers.Contains(userId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task BlockAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_blockedUsers.Add(userId))
                await SaveAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UnblockAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (!_blockedUsers.Remove(userId))
                return false;

            await SaveAsync(cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<long>> GetBlockedUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            return _blockedUsers.OrderBy(id => id).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            _blockedUsers.OrderBy(id => id).ToList(),
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
