namespace AlpacaDashboard;

public class BotListUpdatedEventArgs : EventArgs
{
    public Dictionary<IAsset, IPosition?> ListOfsymbolAndPosition { get; set; } = new();
}
