namespace AlpacaDashboard.Bots;

internal class TakeProfitOrLoss : IBot
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
        BotListUpdated?.Invoke(this, e);
    }
    #endregion

    #region properites that will be shown on UI
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

    //Qty
    private int _qtyToBuy = 1;
    public int QtyToBuy { get => _qtyToBuy; set => _qtyToBuy = value; }

    //profit percentage
    private decimal _profitPercent = 0.5M;
    public decimal ProfitPercent { get => _profitPercent; set => _profitPercent = value; }

    //Loss percentage
    private decimal _LossPercent = 1.0M;
    public decimal LossPercent { get => _LossPercent; set => _LossPercent = value; }

    #endregion

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="broker"></param>
    public TakeProfitOrLoss(Broker broker)
    {
        Broker = broker;
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
        IStock? stock = Broker.StockObjects.GetStock(asset);

        //Run you bot logic until cancelled
        if (stock != null)
        {
            await Task.Run(() => BotStartCall(stock, new BarTimeFrame(BarTimeFrameCount, BarTimeFrameUnit), AverageBars, source.Token), source.Token).ConfigureAwait(false);
        }

        return source;
    }

    /// <summary>
    /// Bot ending logic
    /// </summary>
    /// <param name="source"></param>
    public void End(CancellationTokenSource? source)
    {
        if (source != null)
            source.Cancel();
    }

    /// <summary>
    /// Bot start call and get updated stock for every bar time frame
    /// </summary>
    /// <param name="barTimeFrameCount"></param>
    /// <param name="token"></param>
    private async void BotStartCall(IStock stock, BarTimeFrame barTimeFrame, int averageBars, CancellationToken token)
    {
        //create a log for this bot and symbol
        var log = new LoggerConfiguration()
                  .WriteTo.File("Logs\\" + Broker.Environment.ToString() + "_" + this.GetType()+"_"+stock?.Asset?.Symbol+".log", rollingInterval: RollingInterval.Day)
                  .CreateLogger();

        log.Information($"Starting {this.GetType()} Bot for {stock?.Asset?.Symbol}");

        List<decimal?> closingPrices = new();
        IStock? updatedStock = null;
        try
        {
            var timeUtc = DateTime.UtcNow;
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);

            //do while its not ended
            while (!token.IsCancellationRequested)
            {
                //get historical bars
                var bars = await Broker.GetHistoricalBar(stock?.Asset, barTimeFrame, averageBars, easternTime);
                closingPrices = bars.Select(x => x?.Close).ToList();

                //get stock object of the symbol
                updatedStock = Broker.StockObjects.GetStock(stock?.Asset?.Symbol);

                //your main bot logic here
                /////////////////////////////////////////////////////////////////////////////////

                updatedStock = await Logic(log, closingPrices, updatedStock).ConfigureAwait(false);

                /////////////////////////////////////////////////////////////////////////////////

                closingPrices.Add(updatedStock?.MinuteBar?.Close);
                if (closingPrices.Count > AverageBars)
                {
                    closingPrices.RemoveAt(0);
                }

                var looptime = 0;
                if (barTimeFrame.Unit == BarTimeFrameUnit.Minute) looptime = barTimeFrame.Value;
                if (barTimeFrame.Unit == BarTimeFrameUnit.Hour) looptime = barTimeFrame.Value * 60;
                if (barTimeFrame.Unit == BarTimeFrameUnit.Day) looptime = barTimeFrame.Value * 60 * 24;
                await Task.Delay(TimeSpan.FromMinutes(looptime), token);
            }
        }
        catch (Exception ex)
        {
            var x = ex;
            log.Information($"Ending {this.GetType()} Bot for {stock?.Asset?.Symbol}");
        }
    }

    /// <summary>
    /// Your main mean reversion logic
    /// </summary>
    /// <param name="log"></param>
    /// <param name="scale"></param>
    /// <param name="closingPrices"></param>
    /// <param name="updatedStock"></param>
    /// <param name="lastTradeOpen"></param>
    /// <param name="lastTradeId"></param>
    /// <returns></returns>
    private async Task<IStock?> Logic(Serilog.Core.Logger log,  List<decimal?> closingPrices, IStock? updatedStock)
    {
        //close price
        var close = updatedStock?.Trade?.Price ?? 0M;
        if (close == 0)
            return updatedStock;

        var price = 0M;
        price = updatedStock?.Quote?.AskPrice ?? 0M;
        if (price == 0)
            return updatedStock;

        //symbol
        var symbol = updatedStock?.Asset?.Symbol;
        
        //profit and lost limit prices
        var takeProfit = 0M;
        var takeLoss = 0M;
        takeProfit = (decimal)(close + close * ProfitPercent / 100);
        takeLoss = (decimal)(close - close * LossPercent / 100);

        //last trade open and its id
        bool lastTradeOpen = updatedStock?.OpenOrders.Count != 0 ? true : false;
        IOrder? lastOrder = updatedStock?.OpenOrders.LastOrDefault();

        //current position
        var position = updatedStock?.Position == null ? 0 : updatedStock?.Position.Quantity;

        //calculate average price
        var avg = closingPrices.Average() ?? 0M;
        var diff = avg - close;

        //shortable
        var isAssetShortable = updatedStock?.Asset?.Shortable;

        //assetclass
        var asset = updatedStock?.Asset;

        //assetclass
        var assetClass = updatedStock?.Asset?.Class;

        // Make sure we know how much we should spend on our position.
        var account = await Broker.GetAccountDetails();
        var buyingPower = account?.BuyingPower *.10M ?? 0M;
        var equity = account?.Equity;
        var multiplier = ((decimal?)account?.Multiplier);

        // Check how much we currently have in this position.
        var positionQuantity = updatedStock?.Position?.Quantity ?? 0M;
        var positionValue = updatedStock?.Position?.MarketValue ?? 0M;

        // Allocate a percent of portfolio 
        var amountToLong = buyingPower;

        //calculate quantity
        var calculatedQty = 0M;
        OrderType preferedOrderType = OrderType.Limit;
        TimeInForce timeInForce = TimeInForce.Gtc;
        bool extendedHours = false;

        if (symbol != null)
        {
            if (!lastTradeOpen)
            {
                //calculate quantity to buy
                if (QtyToBuy * price < buyingPower)
                    calculatedQty = QtyToBuy;
                else
                    (calculatedQty, preferedOrderType, timeInForce, extendedHours) = await CalculateQuantity(assetClass, buyingPower, price, preferedOrderType);
                if (calculatedQty == 0)
                    return updatedStock;

                if (calculatedQty > 0 && position == 0)
                {
                    (IOrder? order, string? message) = await Broker.SubmitBracketOrder(GetType().ToString(), OrderSide.Buy, preferedOrderType, timeInForce, extendedHours,
                    asset, OrderQuantity.Fractional(calculatedQty), close, (decimal)takeProfit, takeLoss, takeLoss).ConfigureAwait(false);

                    log.Information($"{message}");
                }
            }
            else
            {
                if (lastOrder != null && position == 0)
                {
                    (IOrder? order, string? message) =  await Broker.ReplaceOpenOrder(GetType().ToString(), (Guid)lastOrder.OrderId, close, null);
                    log.Information($"{message}");
                }
            }
        }
        return updatedStock;
    }
    /// <summary>
    /// UsEquity  if calculated qty is fraction change order type to market else whatever is passed
    /// </summary>
    /// <param name="assetClass"></param>
    /// <param name="amount"></param>
    /// <param name="close"></param>
    /// <param name="inputOrderType"></param>
    /// <returns></returns>
    private async Task<(decimal, OrderType, TimeInForce, bool)> CalculateQuantity(AssetClass? assetClass, decimal amount, decimal close, OrderType inputOrderType)
    {

        bool extendedHours = false;
        var calculatedQty = 0M;
        calculatedQty = Math.Round(amount / close, 2);
        OrderType orderType = inputOrderType;
        TimeInForce timeInForce = TimeInForce.Gtc;
        if (assetClass == AssetClass.UsEquity)
        {
            bool marketOpen = await Broker.IsMarketOpen() ?? false;
            if (!marketOpen)
            {
                orderType = OrderType.Limit;
                if (calculatedQty % 1 != 0)
                {
                    calculatedQty = (Int64)calculatedQty;
                }
                extendedHours = true;
                timeInForce = TimeInForce.Day;
            }
            else
            {
                calculatedQty = amount / close;
                if (calculatedQty % 1 != 0)
                {
                    orderType = OrderType.Market;
                    extendedHours = false;
                    timeInForce = TimeInForce.Day;
                }
            }
        }
        return (calculatedQty, orderType, timeInForce, extendedHours);
    }

}
