using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public class ClientService
{
    readonly IDbContextFactory<TimeBridgeDbContext> _factory;
    readonly IClock _clock;
    public ClientService(IDbContextFactory<TimeBridgeDbContext> factory, IClock clock) { _factory = factory; _clock = clock; }

    public event EventHandler? Changed;

    public async Task<List<Client>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
    }

    public async Task<Client?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    static void Validate(string name, string city, string tz)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Client name is required.");
        if (string.IsNullOrWhiteSpace(city)) throw new ValidationException("City is required.");
        if (TimeZoneCatalog.TryFind(tz) == null) throw new ValidationException("Choose a valid time zone.");
    }

    public async Task<Client> AddAsync(string name, string city, string timeZoneId)
    {
        Validate(name, city, timeZoneId);
        await using var db = await _factory.CreateDbContextAsync();
        var max = await db.Clients.Select(c => (int?)c.SortOrder).MaxAsync() ?? -1;
        var c = new Client
        {
            Name = name.Trim(), City = city.Trim(), TimeZoneId = TimeZoneCatalog.Find(timeZoneId).Id,
            SortOrder = max + 1, CreatedUtc = _clock.UtcNow
        };
        db.Clients.Add(c);
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
        return c;
    }

    public async Task UpdateAsync(int id, string name, string city, string timeZoneId)
    {
        Validate(name, city, timeZoneId);
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Clients.FirstOrDefaultAsync(x => x.Id == id) ?? throw new ValidationException("Client not found.");
        c.Name = name.Trim(); c.City = city.Trim(); c.TimeZoneId = TimeZoneCatalog.Find(timeZoneId).Id;
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deletes the client and (by cascade) their meetings and reminders.</summary>
    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return;
        db.Clients.Remove(c);
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<int> CountMeetingsAsync(int clientId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Meetings.CountAsync(m => m.ClientId == clientId);
    }
}
