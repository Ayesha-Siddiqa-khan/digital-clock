using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

/// <summary>Edit dialog opened from the widget: time zones and widget settings.</summary>
public partial class WidgetEditViewModel : ObservableObject
{
    readonly SettingsService _settings;
    readonly ClientService _clients;
    readonly WidgetViewModel _widget;

    public WidgetEditViewModel(SettingsService settings, ClientService clients, WidgetViewModel widget)
    { _settings = settings; _clients = clients; _widget = widget; }

    public IReadOnlyList<ZoneOption> Zones { get; } = TimeZoneCatalog.All();
    public List<Client> ClientChoices { get; private set; } = new();

    [ObservableProperty] string _homeZoneId = TimeZoneCatalog.PakistanId;
    [ObservableProperty] int? _clientId;
    [ObservableProperty] string? _clientZoneId;
    [ObservableProperty] bool _showSeconds = true;
    [ObservableProperty] double _opacity = 0.96;
    [ObservableProperty] string _error = "";

    public event Action? Saved;

    partial void OnClientIdChanged(int? value)
    {
        ClientZoneId = ClientChoices.FirstOrDefault(c => c.Id == value)?.TimeZoneId;
    }

    public async Task LoadAsync()
    {
        var s = _settings.Current;
        HomeZoneId = s.HomeTimeZoneId; ShowSeconds = s.ShowSeconds; Opacity = s.WidgetOpacity;
        ClientChoices = await _clients.GetAllAsync();
        OnPropertyChanged(nameof(ClientChoices));
        ClientId = _widget.Selected?.Id ?? ClientChoices.FirstOrDefault()?.Id;
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        Error = "";
        try
        {
            if (ClientId is int id && ClientChoices.FirstOrDefault(c => c.Id == id) is { } c && ClientZoneId != null)
            {
                if (!string.Equals(c.TimeZoneId, ClientZoneId, StringComparison.OrdinalIgnoreCase))
                    await _clients.UpdateAsync(c.Id, c.Name, c.City, ClientZoneId);
                _widget.SelectClientById(id);
            }
            var d = _settings.Current.Clone();
            d.HomeTimeZoneId = HomeZoneId; d.ShowSeconds = ShowSeconds; d.WidgetOpacity = Math.Round(Opacity, 2);
            await _settings.SaveAsync(d);
            Saved?.Invoke();
        }
        catch (ValidationException ex) { Error = ex.Message; }
    }
}
