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
    private int _scale = 200;
    public int Scale { get => _scale; set => _scale = value; }
    #endregion

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
    /// <param name="stock"></param>
    /// <param name="barTimeFrameUnit"></param>
    /// <param name="barTimeFrameCount"></param>
    /// <param name="token"></param>
    private async void BotStartCall(IStock stock, BarTimeFrame barTimeFrame, int averageBars, int scale, CancellationToken token)
    {
        //create a log for this bot and symbol
        var log = new LoggerConfiguration()
                  .WriteTo.File("Logs\\"+this.GetType()+"_"+stock?.Asset?.Symbol+".log", rollingInterval: RollingInterval.Day)
                  .CreateLogger();

        log.Information($"Starting {this.GetType()} Bot for {stock?.Asset?.Symbol}");

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
                //get update stock data for every loop
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
        var symbol = updatedStock?.Asset?.Symbol;
        var close = updatedStock?.MinuteBar?.Close==null ? updatedStock?.Trade?.Price : updatedStock?.MinuteBar?.Close;
        var avg = closingPrices.Average();
        var diff = avg - close;
        var isAssetShortable = updatedStock?.Asset?.Shortable;
        var assetClass = updatedStock?.Asset?.Class;

        bool lastTradeOpen = false;
        Guid? lastTradeId = Guid.NewGuid();
        switch (updatedStock?.TradeUpdate?.Event)
        {
            case TradeEvent.Fill:
                log.Information($"{symbol} Trade filled");
                Console.WriteLine("Trade filled.");
                lastTradeOpen = false;
                break;
            case TradeEvent.Rejected:
                log.Information($"{symbol} Trade Rejected");
                lastTradeOpen = false;
                break;
            case TradeEvent.Canceled:
                log.Information($"{symbol} Trade Canceled");
                lastTradeOpen = false;
                break;
        }

        // If the last trade hasn't filled yet, we'd rather replace
        // it than have two orders open at once.
        if (lastTradeOpen && lastTradeId!=null)
        {
            // We need to wait for the cancel to process in order to avoid
            // having long and short orders open at the same time.
            var res = await Broker.DeleteOpenOrder((Guid)lastTradeId);
        }

        // Make sure we know how much we should spend on our position.
        var account = await Broker.GetAccountDetails();
        var buyingPower = account?.BuyingPower * 0.10M ?? 0M;
        var equity = account?.Equity;
        var multiplier = ((decimal?)account?.Multiplier);


        // Check how much we currently have in this position.
        var positionQuantity = updatedStock?.Position?.Quantity == null ? 0M : updatedStock?.Position?.Quantity;
        var positionValue = updatedStock?.Position?.MarketValue == null ? 0M : updatedStock?.Position?.MarketValue;

        //price is above average
        if (diff < 0)
        {
            if (positionQuantity > 0)
            {
                //close existing long position
                if (symbol != null)
                {
                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Sell, OrderType.Limit, TimeInForce.Gtc, false,
                    symbol, OrderQuantity.Fractional((decimal)positionQuantity), null, close,
                    null, null).ConfigureAwait(false);
                    lastTradeId = order?.OrderId;
                    lastTradeOpen = order==null ? false : true;

                    log.Information($"Closing exiting long {positionQuantity} position : {message}");
                }
            }
            else
            {
                // Allocate a percent of portfolio to short position
                var portfolioShare = -1 * diff / close * scale;
                var targetPositionValue = -1 * equity * multiplier * portfolioShare;
                var amountToShort = targetPositionValue - positionValue;

                switch (amountToShort)
                {
                    case < 0:
                        {
                            // We want to expand our existing short position.
                            //get amount to short
                            if (amountToShort > account?.BuyingPower)
                            {
                                amountToShort = (decimal)account.BuyingPower;
                            }


                            //calulate quantity
                            decimal calculatedQty = CalculateQuantity(close, assetClass, amountToShort);

                            if (isAssetShortable == true)
                            {
                                if (updatedStock?.Asset?.Symbol != null)
                                {
                                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Buy, OrderType.Limit, TimeInForce.Gtc, false,
                                    updatedStock.Asset.Symbol, OrderQuantity.Fractional(calculatedQty), null, close,
                                    null, null).ConfigureAwait(false);
                                    lastTradeId = order?.OrderId;
                                    lastTradeOpen = order == null ? false : true;

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
                            decimal calculatedQty = CalculateQuantity(close, assetClass, amountToShort);

                            // We want to shrink our existing short position.
                            if (positionQuantity != null)
                            {
                                if (calculatedQty > -1 * positionQuantity)
                                {
                                    calculatedQty = (decimal)(-1 * positionQuantity);
                                }
                            }

                            if (symbol != null)
                            {
                                (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Buy, OrderType.Limit, TimeInForce.Gtc, false,
                                symbol, OrderQuantity.Fractional(calculatedQty), null, close,
                                null, null).ConfigureAwait(false);
                                lastTradeId = order?.OrderId;
                                lastTradeOpen = order == null ? false : true;

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
            var targetPositionValue = equity * multiplier * portfolioShare;
            var amountToLong = targetPositionValue - positionValue;

            if (positionQuantity < 0)
            {
                //close exising short position
                if (symbol != null)
                {
                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Buy, OrderType.Limit, TimeInForce.Gtc, false,
                    symbol, OrderQuantity.Fractional(-1 * (decimal)positionQuantity), null, close,
                    null, null).ConfigureAwait(false);
                    lastTradeId = order?.OrderId;
                    lastTradeOpen = order == null ? false : true;

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
                            decimal calculatedQty = CalculateQuantity(close, assetClass, amountToLong);

                            if (symbol != null)
                            {
                                (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Buy, OrderType.Limit, TimeInForce.Gtc, false,
                                symbol, OrderQuantity.Fractional(calculatedQty), null, close,
                                null, null).ConfigureAwait(false);
                                lastTradeId = order?.OrderId;
                                lastTradeOpen = order == null ? false : true;

                                log.Information($"Adding {calculatedQty * close:C2} to long position : {message}");
                            }
                            break;
                        }

                    case < 0:
                        {
                            // We want to shrink our existing long position.
                            amountToLong *= -1;

                            //calulate quantity
                            decimal calculatedQty = CalculateQuantity(close, assetClass, amountToLong);

                            if (calculatedQty > positionQuantity)
                            {
                                calculatedQty = (decimal)positionQuantity;
                            }

                            if (isAssetShortable == true)
                            {
                                if (updatedStock?.Asset?.Symbol != null)
                                {
                                    (IOrder? order, string? message) = await Broker.SubmitOrder(OrderSide.Sell, OrderType.Limit, TimeInForce.Gtc, false,
                                    updatedStock.Asset.Symbol, OrderQuantity.Fractional(calculatedQty), null, close,
                                    null, null).ConfigureAwait(false);
                                    lastTradeId = order?.OrderId;
                                    lastTradeOpen = order == null ? false : true;

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

    private static decimal CalculateQuantity(decimal? close, AssetClass? assetClass, decimal? amount)
    {
        var calculatedQty = 0M;
        if (assetClass == AssetClass.UsEquity)
        {
            if (amount != null && close != null)
                calculatedQty = (Int64)(amount / close);
        }
        if (assetClass == AssetClass.Crypto)
        {
            if (amount != null && close != null)
                calculatedQty = (decimal)amount / (decimal)close;
        }

        return calculatedQty;
    }
}
