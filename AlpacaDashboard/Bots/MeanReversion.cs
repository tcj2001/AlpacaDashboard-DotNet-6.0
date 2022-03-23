namespace AlpacaDashboard;

internal class MeanReversion : IBot
{
    #region Required
    //Broker Environment
    public Broker Broker { get; set; } = default!;

    //WatchList
    public IWatchList WatchList { get; set; } = default!;

    //selected symbol
    public string SelectedSymbol { get; set; } = default!;

    //Active symbols
    public Dictionary<string, CancellationTokenSource> ActiveSymbols { get; set; } = new();

    //UI screen container
    public Control UiContainer { get; set; } = new();

    //event hander to indicate scan finished
    public event EventHandler<BotListUpdatedEventArgs> BotListUpdated = default!;

    //list to hold symbol and last bar of the time frame
    public Dictionary<string, IPosition> ListOfSymbolAndPosition { get; set; } = new();

    //TimeFrame unit
    private BarTimeFrameUnit _BarTimeFrameUnit = BarTimeFrameUnit.Minute;
    public BarTimeFrameUnit BarTimeFrameUnit { get => _BarTimeFrameUnit; set => _BarTimeFrameUnit = value; }

    //Required BarTimeFrameUnit 
    private int _BarTimeFrameCount = 1;
    public int BarTimeFrameCount { get => _BarTimeFrameCount; set => _BarTimeFrameCount = value; }

    //Define all other field that need to be shown on the UI
    //none

    /// <summary>
    /// Get a list of bot symbols
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, IPosition> GetBotList()
    {
        return ListOfSymbolAndPosition;
    }

    /// <summary>
    /// event generated for UI when list is updated
    /// </summary>
    /// <param name="e"></param>
    public void OnListUpdated(BotListUpdatedEventArgs e)
    {
        BotListUpdated?.Invoke(this, e);
    }
    #endregion

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="broker"></param>
    public MeanReversion(Broker broker)
    {
        Broker = broker;
        ActiveSymbols = new Dictionary<string, CancellationTokenSource>();    
    }

    /// <summary>
    /// Start Method
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<CancellationTokenSource> Start(string symbol)
    {
        CancellationTokenSource source = new();
        CancellationToken token = source.Token;

        //get stock object of the symbol
        IStock? stock = null;
        if (Broker.Environment == TradingEnvironment.Paper)
        {
            stock = Stock.PaperStockObjects.GetStock(symbol);
        }
        if (Broker.Environment == TradingEnvironment.Live)
        {
            stock = Stock.LiveStockObjects.GetStock(symbol);
        }

        //Run you bot logic until cancelled
        if (stock != null)
        {
            await Task.Run(() => BotLogic(BarTimeFrameCount, source.Token), source.Token).ConfigureAwait(false);
        }

        return source;
    }

    /// <summary>
    /// End Method
    /// </summary>
    /// <param name="source"></param>
    public void End(CancellationTokenSource source) 
    { 
        source.Cancel();
    }

    /// <summary>
    /// Main Bot Logic is in here
    /// </summary>
    /// <param name="barTimeFrameCount"></param>
    /// <param name="token"></param>
    private static async void BotLogic(int barTimeFrameCount, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(barTimeFrameCount), token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
