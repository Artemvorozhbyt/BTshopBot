
using System.Text.Json;

public sealed class KnownUsersService
{
    private readonly string _filePath;
    private readonly HashSet<long> _knownUsers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public KnownUsersService(string filePath)
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
                _knownUsers.UnionWith(users);
        }
    }

    public async Task<bool> TryRegisterAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (!_knownUsers.Add(userId))
                return false;

            var json = JsonSerializer.Serialize(
                _knownUsers.OrderBy(id => id).ToList(),
                new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(
                _filePath,
                json,
                cancellationToken);

            return true;
        }
        catch
        {
            _knownUsers.Remove(userId);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}
