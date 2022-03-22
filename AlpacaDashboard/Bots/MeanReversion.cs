namespace AlpacaDashboard.Bots;

internal class MeanReversion : IBot
{
    #region Required
    //Broker Environment
    public Broker Broker { get; set; } = default!;

    //WatchList
    public IWatchList WatchList { get; set; } = default!;

    //selected symbol
    public IAsset? SelectedAsset { get; set; } = default!;

    //Active symbols
    public Dictionary<IAsset, CancellationTokenSource>? ActiveAssets { get; set; } = new();

    //UI screen container
    public Control UiContainer { get; set; } = new();

    //event hander to indicate scan finished
    public event EventHandler<BotListUpdatedEventArgs> BotListUpdated = default!;

    //list to hold symbol and last bar of the time frame
    public Dictionary<IAsset, IPosition?> ListOfAssetAndPosition { get; set; } = new();

    /// <summary>
    /// Get a list of bot symbols
    /// </summary>
    /// <returns></returns>
    public Dictionary<IAsset, IPosition?> GetBotList()
    {
        return ListOfAssetAndPosition;
    }

    /// <summary>
    /// event generated for UI when list is updated
    /// </summary>
    /// <param name="e"></param>
    public void OnListUpdated(BotListUpdatedEventArgs e)
    {
        EventHandler<BotListUpdatedEventArgs> handler = BotListUpdated;
        if (handler != null)
        {
            handler(this, e);
        }
    }
    #endregion

    //Define all other field that need to be shown on the UI
    //TimeFrame unit
    private BarTimeFrameUnit _BarTimeFrameUnit = BarTimeFrameUnit.Minute;
    public BarTimeFrameUnit BarTimeFrameUnit { get => _BarTimeFrameUnit; set => _BarTimeFrameUnit = value; }

    //Required BarTimeFrameUnit 
    private int _BarTimeFrameCount = 1;
    public int BarTimeFrameCount { get => _BarTimeFrameCount; set => _BarTimeFrameCount = value; }

    //MeanReversionAverageBars
    private int _averageBars = 20;
    public int AverageBars { get => _averageBars; set => _averageBars = value; }


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="broker"></param>
    public MeanReversion(Broker broker)
    {
        this.Broker = broker;
        ActiveAssets = new Dictionary<IAsset, CancellationTokenSource>();
    }

    /// <summary>
    /// Start Method
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<CancellationTokenSource> Start(IAsset asset)
    {
        CancellationTokenSource source = new();
        CancellationToken token = source.Token;

        //get stock object of the symbol
        IStock? stock = null;
        if (Broker.Environment == "Paper")
        {
            stock = Stock.PaperStockObjects.GetStock(asset);
        }
        if (Broker.Environment == "Live")
        {
            stock = Stock.LiveStockObjects.GetStock(asset);
        }

        //Run you bot logic until cancelled
        if (stock != null)
        {
            await Task.Run(() => BotLogic(stock, new BarTimeFrame(BarTimeFrameCount, BarTimeFrameUnit), AverageBars, source.Token), source.Token).ConfigureAwait(false);
        }

        return source;
    }

    /// <summary>
    /// End Method
    /// </summary>
    /// <param name="source"></param>
    public void End(CancellationTokenSource? source)
    {
        if (source != null)
            source.Cancel();
    }

    /// <summary>
    /// Main Bot Logic is in here
    /// </summary>
    /// <param name="stock"></param>
    /// <param name="barTimeFrameUnit"></param>
    /// <param name="barTimeFrameCount"></param>
    /// <param name="token"></param>
    private async void BotLogic(IStock stock, BarTimeFrame barTimeFrame, int averageBars, CancellationToken token)
    {
        List<Decimal?> closingPrices = new();
        IStock? updatedStock = null;
        try
        {
            var timeUtc = DateTime.UtcNow;
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);

            //get historical bars
            var bars = await Broker.GetHistoricalBar(stock?.Asset, barTimeFrame, averageBars, easternTime);
            closingPrices = bars.Select(x => x?.Close).ToList();

            //do while its not ended
            while (!token.IsCancellationRequested)
            {
                //get update stock data 
                if (Broker.Environment == "Paper")
                {
                    updatedStock = Stock.PaperStockObjects.GetStock(stock?.Asset?.Symbol);
                }
                if (Broker.Environment == "Live")
                {
                    updatedStock = Stock.LiveStockObjects.GetStock(stock?.Asset?.Symbol);
                }

                //your main bot logic here
                /////////////////////////////////////////////////////////////////////////////////

                var avg = closingPrices.Average();
                var diff = avg - updatedStock?.MinuteBar?.Close;

                if (diff < 0)
                {
                    //price is above average
                    if (updatedStock?.Position?.Quantity > 0)
                    {
                        if(updatedStock?.Asset?.Symbol!=null)
                            await Broker.SubmitOrder(OrderSide.Sell, OrderType.Limit, TimeInForce.Gtc, true,
                            updatedStock.Asset.Symbol, OrderQuantity.Fractional((decimal)updatedStock.Position.Quantity), null, updatedStock?.MinuteBar?.Close,
                            null, null).ConfigureAwait(false);
                    }
                    else
                    {
                        var account = await Broker.GetAccountDetails();
                        var equity = account?.Equity;
                        var multiplier = ((decimal?)account?.Multiplier);
                        var portfolioShare = -1 * diff / updatedStock?.MinuteBar?.Close * 200;
                        var targetPositionValue = -1 * account?.Equity * multiplier * portfolioShare;
                        var amountToShort = targetPositionValue - updatedStock?.Position?.MarketValue ?? 0M;
                    }
                }


                closingPrices.Add(updatedStock?.MinuteBar?.Close);
                if (closingPrices.Count > BarTimeFrameCount)
                {
                    closingPrices.RemoveAt(0);
                }

                /////////////////////////////////////////////////////////////////////////////////

                var looptime = 0;
                if (barTimeFrame.Unit == BarTimeFrameUnit.Minute) looptime = barTimeFrame.Value;
                if (barTimeFrame.Unit == BarTimeFrameUnit.Hour) looptime = barTimeFrame.Value * 60;
                if (barTimeFrame.Unit == BarTimeFrameUnit.Day) looptime = barTimeFrame.Value * 60 * 24;
                await Task.Delay(TimeSpan.FromMinutes(looptime), token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
