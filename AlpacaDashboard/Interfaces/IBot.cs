global using AlpacaDashboard.Brokers;

namespace AlpacaDashboard;

public interface IBot
{
    //Broker Environment
    Broker Broker { get; set; }

    //WatchList
    IWatchList WatchList { get; set; }

    //selected symbol
    string SelectedSymbol { get; set; }

    //Active symbols
    Dictionary<string, CancellationTokenSource> ActiveSymbols { get; set; }

    //UI screen container
    Control UiContainer { get; set; }

    //event hander to indicate scan finished
    public event EventHandler<BotListUpdatedEventArgs> BotListUpdated;

    //list to hold symbol and last bar of the time frame
    Dictionary<string, IPosition> ListOfSymbolAndPosition { get; set; }
    
    //TimeFrame unit
    BarTimeFrameUnit BarTimeFrameUnit { get; set; }

    //TimeFrame count
    int BarTimeFrameCount { get; set; }

    //Return list of symbol and Last Bar
    Dictionary<string, IPosition> GetBotList();

    //bot list updated event
    void OnListUpdated(BotListUpdatedEventArgs e);

    //start bot
    Task<CancellationTokenSource> Start(string symbol);

    //End Bot
    void End(CancellationTokenSource token);
}
