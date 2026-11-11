using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.App.Services;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

public partial class ClientsViewModel : ObservableObject
{
    readonly ClientService _clients;
    readonly ClockTicker _ticker;
    readonly IDialogService _dialogs;
    readonly WidgetViewModel _widget;

    public ClientsViewModel(ClientService clients, ClockTicker ticker, IDialogService dialogs, WidgetViewModel widget)
    { _clients = clients; _ticker = ticker; _dialogs = dialogs; _widget = widget; }

    public ObservableCollection<ClientClockItem> Items { get; } = new();
    public IReadOnlyList<ZoneOption> Zones { get; } = TimeZoneCatalog.All();

    [ObservableProperty] ClientClockItem? _selectedItem;
    [ObservableProperty] string _name = "";
    [ObservableProperty] string _city = "";
    [ObservableProperty] string? _zoneId = "Europe/London";
    [ObservableProperty] string _error = "";

    public bool IsEditing => SelectedItem != null;
    public string SaveText => IsEditing ? "Save client" : "+ Add client";
    public string FormTitle => IsEditing ? "Edit client" : "New client";

    partial void OnSelectedItemChanged(ClientClockItem? value)
    {
        OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(SaveText)); OnPropertyChanged(nameof(FormTitle));
        Error = "";
        if (value == null) return;
        Name = value.Name; City = value.City; ZoneId = value.Client.TimeZoneId;
    }

    public async Task InitializeAsync()
    {
        await ReloadAsync();
        _clients.Changed += async (_, _) => await Ui.Run(ReloadAsync);
        _ticker.Tick += utc => { foreach (var i in Items) i.Update(utc); };
    }

    async Task ReloadAsync()
    {
        var keep = SelectedItem?.Id;
        var list = await _clients.GetAllAsync();
        Items.Clear();
        foreach (var c in list) { var i = new ClientClockItem(c); i.Update(_ticker.UtcNow); Items.Add(i); }
        SelectedItem = Items.FirstOrDefault(i => i.Id == keep);
    }

    [RelayCommand]
    void New()
    {
        SelectedItem = null; Name = ""; City = ""; ZoneId = "Europe/London"; Error = "";
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        Error = "";
        try
        {
            if (SelectedItem is { } sel)
            {
                await _clients.UpdateAsync(sel.Id, Name, City, ZoneId ?? "");
            }
            else
            {
                var c = await _clients.AddAsync(Name, City, ZoneId ?? "");
                if (Items.Count <= 1) _widget.SelectClientById(c.Id);
                New();
            }
        }
        catch (ValidationException ex) { Error = ex.Message; }
    }

    [RelayCommand]
    async Task DeleteAsync(ClientClockItem? item)
    {
        item ??= SelectedItem;
        if (item == null) return;
        var count = await _clients.CountMeetingsAsync(item.Id);
        var extra = count > 0 ? $"\n\nThis also deletes {count} meeting(s) and their alarms." : "";
        if (!_dialogs.Confirm($"Delete client \"{item.Label}\"?{extra}", "Delete client")) return;
        await _clients.DeleteAsync(item.Id);
        New();
    }

    [RelayCommand]
    void UseInWidget(ClientClockItem? item)
    {
        item ??= SelectedItem;
        if (item != null) _widget.SelectClientById(item.Id);
    }
}
