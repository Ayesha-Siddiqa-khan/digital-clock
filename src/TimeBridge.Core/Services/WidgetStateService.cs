using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

public class WidgetStateService
{
    readonly IDbContextFactory<TimeBridgeDbContext> _factory;
    public WidgetStateService(IDbContextFactory<TimeBridgeDbContext> factory) => _factory = factory;

    public async Task<WidgetState> LoadAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.WidgetState.AsNoTracking().FirstOrDefaultAsync(w => w.Id == 1) ?? new WidgetState();
    }

    public async Task SaveAsync(WidgetState state)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.WidgetState.FirstOrDefaultAsync(w => w.Id == 1);
        if (row == null) { row = new WidgetState { Id = 1 }; db.WidgetState.Add(row); }
        row.Left = state.Left; row.Top = state.Top;
        row.Width = state.Width; row.Height = state.Height;
        row.IsPinned = state.IsPinned; row.IsCollapsed = state.IsCollapsed;
        row.IsVisible = state.IsVisible;
        row.SelectedClientId = state.SelectedClientId;
        await db.SaveChangesAsync();
    }
}
