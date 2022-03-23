namespace AlpacaDashboard;

public class BotListUpdatedEventArgs : EventArgs
{
    public Dictionary<string, IPosition> ListOfsymbolAndPosition { get; set; } = new();
}
