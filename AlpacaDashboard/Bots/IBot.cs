global using AlpacaDashboard.Brokers;

namespace AlpacaDashboard;

public interface IBot
{
    //Broker Environment
    Broker Broker { get; set; }

    //WatchList
    IWatchList WatchList { get; set; }

    //selected symbol
    IAsset? SelectedAsset { get; set; }

    //Active symbols
    Dictionary<IAsset, CancellationTokenSource>? ActiveAssets { get; set; }

    //UI screen container
    Control UiContainer { get; set; }

    //event hander to indicate scan finished
    public event EventHandler<BotListUpdatedEventArgs> BotListUpdated;

    //list to hold symbol and last bar of the time frame
    Dictionary<IAsset, IPosition?> ListOfAssetAndPosition { get; set; }
    
    //Return list of symbol and Last Bar
    Dictionary<IAsset, IPosition?> GetBotList();

    //bot list updated event
    void OnListUpdated(BotListUpdatedEventArgs e);

    //start bot
    Task<CancellationTokenSource> Start(IAsset asset);

    //End Bot
    void End(CancellationTokenSource token);
    
}
