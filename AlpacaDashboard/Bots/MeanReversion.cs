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

    //Scale
    private int _scale = 10;
    public int Scale { get => _scale; set => _scale = value; }
    #endregion

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="broker"></param>
    public MeanReversion(Broker broker)
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
            await Task.Run(() => BotStartCall(stock, new BarTimeFrame(BarTimeFrameCount, BarTimeFrameUnit), AverageBars, Scale, source.Token), source.Token).ConfigureAwait(false);
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
    private async void BotStartCall(IStock stock, BarTimeFrame barTimeFrame, int averageBars, int scale, CancellationToken token)
    {
        //create a log for this bot and symbol
        var log = new LoggerConfiguration()
                  .WriteTo.File("Logs\\"+this.GetType()+"_"+stock?.Asset?.Symbol+".log", rollingInterval: RollingInterval.Day)
                  .CreateLogger();

        log.Information($"Starting {this.GetType()} Bot for {stock?.Asset?.Symbol}");

        List<decimal?> closingPrices = new();
        IStock? updatedStock = null;
        try
        {
            var timeUtc = DateTime.UtcNow;
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);

            //get historical bars
            var bars = await Broker.GetHistoricalBar(stock?.Asset, barTimeFrame, averageBars, easternTime);
               
            closingPrices = bars.Select(x => x?.Close).ToList();

            //cancel all existing open orders
            await Broker.DeleteOpenOrders(stock?.Asset?.Symbol);
            log.Information($"Closing any open orders for {stock?.Asset?.Symbol}");

            //do while its not ended
            while (!token.IsCancellationRequested)
            {
                //get stock object of the symbol
                updatedStock = Broker.StockObjects.GetStock(stock?.Asset?.Symbol);

                //your main bot logic here
                /////////////////////////////////////////////////////////////////////////////////

                updatedStock = await MeanReversionLogic(log, scale, closingPrices, updatedStock).ConfigureAwait(false);

                /////////////////////////////////////////////////////////////////////////////////

                closingPrices.Add(updatedStock?.MinuteBar?.Close);
                if (closingPrices.Count > BarTimeFrameCount)
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
    private async Task<IStock?> MeanReversionLogic(Serilog.Core.Logger log, int scale,  List<decimal?> closingPrices, IStock? updatedStock)
    {
        //wait till minute bar is populated
        if (updatedStock?.MinuteBar == null)
            return updatedStock;

        //close price
        var close = updatedStock?.Trade?.Price ?? 0M;
        if (close == 0)
            return updatedStock;

        //symbol
        var symbol = updatedStock?.Asset?.Symbol;

        //last trade open and its id
        bool lastTradeOpen = updatedStock?.OrdersWithItsOldOrderId.Count > 0 ? true : false;
        Guid? lastTradeId = updatedStock?.OrdersWithItsOldOrderId.Values.LastOrDefault();

        //calculate average price
        var avg = closingPrices.Average();
        var diff = avg - close;

        //shortable
        var isAssetShortable = updatedStock?.Asset?.Shortable;

        //assetclass
        var asset = updatedStock?.Asset;

        //assetclass
        var assetClass = updatedStock?.Asset?.Class;

        //if there is open order and and we have a tradeupdate event
        if (lastTradeOpen && lastTradeId != null)
        {
            var res = await Broker.DeleteOpenOrder((Guid)lastTradeId);
            log.Information($"Closing Open Order {lastTradeId} of Qty {updatedStock?.TradeUpdate?.Order.Quantity}");
        }

        // Make sure we know how much we should spend on our position.
        var account = await Broker.GetAccountDetails();
        var buyingPower = account?.BuyingPower * .10M ?? 0M;
        var equity = account?.Equity;
        var multiplier = ((decimal?)account?.Multiplier);

        // Check how much we currently have in this position.
        var positionQuantity = updatedStock?.Position?.Quantity ?? 0M;
        var positionValue = updatedStock?.Position?.MarketValue ?? 0M;

        //price is above average
        if (diff <= 0)
        {
            if (positionQuantity > 0)
            {
                //close existing long position
                if (symbol != null)
                {
                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Sell, OrderType.Limit, TimeInForce.Gtc, false,
                    asset, OrderQuantity.Fractional((decimal)positionQuantity), null, close,
                    null, null).ConfigureAwait(false);

                    log.Information($"Closing exiting long {positionQuantity} position : {message}");
                }
            }
            else
            {
                // Allocate a percent of portfolio to short position
                var portfolioShare = -diff / close * scale;
                var amountToShort = buyingPower * portfolioShare ?? 0M;

                switch (amountToShort)
                {
                    case < 0:
                        {
                            // We want to expand our existing short position.
                            //get amount to short
                            amountToShort *= -1;
                            if (amountToShort > account?.BuyingPower)
                            {
                                amountToShort = (decimal)account.BuyingPower;
                            }


                            //calulate quantity
                            decimal calculatedQty = CalculateQuantity(assetClass, amountToShort, close);

                            if (isAssetShortable == true)
                            {
                                if (updatedStock?.Asset?.Symbol != null)
                                {
                                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Sell, OrderType.Limit, TimeInForce.Gtc, false,
                                    asset, OrderQuantity.Fractional(calculatedQty), null, close,
                                    null, null).ConfigureAwait(false);

                                    log.Information($"Adding {calculatedQty * close:C2} to short position : {message}");
                                }
                            }
                            else
                            {
                                log.Information($"Unable to place short order - asset is not shortable.");
                            }
                            break;
                        }

                    case > 0:
                        {

                            //calulate quantity
                            decimal calculatedQty = CalculateQuantity(assetClass, amountToShort, close);

                            // We want to shrink our existing short position.
                            if (calculatedQty > -1 * positionQuantity)
                            {
                                calculatedQty = (decimal)(-1 * positionQuantity);
                            }

                            if (symbol != null)
                            {
                                (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Buy, OrderType.Limit, TimeInForce.Gtc, false,
                                asset, OrderQuantity.Fractional(calculatedQty), null, close,
                                null, null).ConfigureAwait(false);

                                log.Information($"Removing {calculatedQty * close:C2} from short position : {message}");
                            }
                            break;
                        }
                }
            }
        }
        //price is below average
        else
        {
            var portfolioShare = diff / close * scale;
            var amountToLong = buyingPower * portfolioShare ?? 0M;

            if (positionQuantity < 0)
            {
                //close exising short position
                if (symbol != null)
                {
                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Buy, OrderType.Limit, TimeInForce.Gtc, false,
                    asset, OrderQuantity.Fractional(-1 * (decimal)positionQuantity), null, close,
                    null, null).ConfigureAwait(false);

                    log.Information($"Removing {positionValue:C2} short position : {message}");
                }
            }
            else
            {
                switch (amountToLong)
                {
                    case > 0:
                        {
                            // We want to expand our existing long position.
                            if (amountToLong > buyingPower)
                            {
                                amountToLong = (decimal)buyingPower;
                            }

                            //calulate quantity
                            decimal calculatedQty = CalculateQuantity(assetClass, amountToLong, close);

                            if (symbol != null)
                            {
                                (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Buy, OrderType.Limit, TimeInForce.Gtc, false,
                                asset, OrderQuantity.Fractional(calculatedQty), null, close,
                                null, null).ConfigureAwait(false);

                                log.Information($"Adding {calculatedQty * close:C2} to long position : {message}");
                            }
                            break;
                        }

                    case < 0:
                        {
                            // We want to shrink our existing long position.
                            amountToLong *= -1;

                            //calulate quantity
                            decimal calculatedQty = CalculateQuantity(assetClass, amountToLong, close);

                            if (calculatedQty > positionQuantity)
                            {
                                calculatedQty = (decimal)positionQuantity;
                            }

                            if (isAssetShortable == true)
                            {
                                if (updatedStock?.Asset?.Symbol != null)
                                {
                                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Sell, OrderType.Limit, TimeInForce.Gtc, false,
                                    asset, OrderQuantity.Fractional(calculatedQty), null, close,
                                    null, null).ConfigureAwait(false);

                                    log.Information($"Removing {calculatedQty * close:C2} from long position : {message}");
                                }
                            }
                            else
                            {
                                log.Information($"Unable to place short order - asset is not shortable.");
                            }
                            break;
                        }
                }
            }
        }
        return updatedStock;
    }

    private static decimal CalculateQuantity(AssetClass? assetClass, decimal amount, decimal close)
    {
        var calculatedQty = 0M;
        if (assetClass == AssetClass.UsEquity)
        {
            calculatedQty = (Int64)(amount / close);
        }
        if (assetClass == AssetClass.Crypto)
        {
            calculatedQty = amount / close;
        }

        return Math.Round(calculatedQty, 2);
    }
}
