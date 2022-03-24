namespace AlpacaDashboard;

public class Stock : IStock
{
    public static Broker Broker { get; set; } = default!;
    public static StockList PaperStockObjects = new();
    public static StockList LiveStockObjects = new();
    public static StockList StockObjects = new();
    public IAsset? Asset { get; set; }
    public IQuote? Quote { get; set; }
    public ITrade? Trade { get; set; }
    public IPosition? Position { get; set; }
    public ITradeUpdate? TradeUpdate { get; set; }
    public IBar? MinuteBar { get; set; }
    public bool subscribed { get; set; }
    public bool lastTradeOpen { get; set; }
    public object Tag { get; set; }

    public static bool MinutesBarSubscribed = false;

    public Stock(Broker broker, IAsset asset, string type)
    {
        Asset = asset;
        Tag = type;

        IStock? stock = null;
        if (broker.Environment == TradingEnvironment.Live)
        {
            stock = LiveStockObjects.GetStock(asset.Symbol);
            if (stock == null)
            {
                LiveStockObjects.Add(this);
            }
        }

        if (broker.Environment == TradingEnvironment.Paper && PaperStockObjects != null)
        {
            stock = PaperStockObjects.GetStock(asset.Symbol);

            if (stock == null)
            {
                PaperStockObjects.Add(this);
            }
        }

    }

    /// <summary>
    /// subscribe symbol for trade,quote and minutebar subscription
    /// </summary>
    /// <param name="broker"></param>
    /// <param name="symbol"></param>
    /// <returns></returns>
    static public async Task Subscribe(Broker broker, string symbol, string watchListCategory)
    {
        IAlpacaDataSubscription<ITrade>? tradeSubscription = null;
        IAlpacaDataSubscription<IBar>? barSubscription = null;
        IAlpacaDataSubscription<IQuote>? quoteSubscription = null;

        IAsset asset = await broker.GetAsset(symbol);
        IStock? stock = null;
        if (asset != null)
        {
            if (broker.Environment == TradingEnvironment.Live)
            {
                stock = LiveStockObjects.GetStock(symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, watchListCategory);
                }
            }
            if (broker.Environment == TradingEnvironment.Paper)
            {
                stock = PaperStockObjects.GetStock(symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, watchListCategory);
                }
            }

            if (stock?.subscribed==false)
            {
                if (broker.subscribed)
                {

                    if (asset.Class == AssetClass.Crypto)
                    {
                        tradeSubscription = Broker.AlpacaCryptoStreamingClient.GetTradeSubscription(symbol);
                        tradeSubscription.Received += CryptoTradeSubscription_Received;
                        await Broker.AlpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription);

                        quoteSubscription = Broker.AlpacaCryptoStreamingClient.GetQuoteSubscription(symbol);
                        quoteSubscription.Received += CryptoQuoteSubscription_Received;
                        await Broker.AlpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription);

                        barSubscription = Broker.AlpacaCryptoStreamingClient.GetMinuteBarSubscription(symbol);
                        barSubscription.Received += CryptoMinAggrSubscription_Received;
                        await Broker.AlpacaCryptoStreamingClient.SubscribeAsync(barSubscription);
                    }

                    if (broker.Environment == TradingEnvironment.Live)
                    {
                        if (asset.Class == AssetClass.UsEquity)
                        {
                            tradeSubscription = broker.AlpacaDataStreamingClient.GetTradeSubscription(symbol);
                            tradeSubscription.Received += UsEquityLiveTradeSubscription_Received;
                            await broker.AlpacaDataStreamingClient.SubscribeAsync(tradeSubscription);

                            quoteSubscription = broker.AlpacaDataStreamingClient.GetQuoteSubscription(symbol);
                            quoteSubscription.Received += UsEquityLiveQuoteSubscription_Received;
                            await broker.AlpacaDataStreamingClient.SubscribeAsync(quoteSubscription);

                            barSubscription = broker.AlpacaDataStreamingClient.GetMinuteBarSubscription(symbol);
                            barSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
                            await broker.AlpacaDataStreamingClient.SubscribeAsync(barSubscription);
                        }
                    }

                    if (broker.Environment == TradingEnvironment.Paper)
                    {
                        if (asset.Class == AssetClass.UsEquity)
                        {
                            tradeSubscription = broker.AlpacaDataStreamingClient.GetTradeSubscription(symbol);
                            tradeSubscription.Received += UsEquityPaperTradeSubscription_Received;
                            await broker.AlpacaDataStreamingClient.SubscribeAsync(tradeSubscription);

                            quoteSubscription = broker.AlpacaDataStreamingClient.GetQuoteSubscription(symbol);
                            quoteSubscription.Received += UsEquityPaperQuoteSubscription_Received;
                            await broker.AlpacaDataStreamingClient.SubscribeAsync(quoteSubscription);

                            barSubscription = broker.AlpacaDataStreamingClient.GetMinuteBarSubscription(symbol);
                            barSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
                            await broker.AlpacaDataStreamingClient.SubscribeAsync(barSubscription);
                        }
                    }
                }
                if (stock != null)
                    stock.subscribed = true;
            }
        }
    }

    /// <summary>
    /// subscribe symbols for trade,quote and minutebar subscription
    /// </summary>
    /// <param name="broker"></param>
    /// <param name="symbols"></param>
    /// <returns></returns>
    static public async Task Subscribe(Broker broker, IEnumerable<IAsset> assets, int maxSymbolsAtOnetime, string watchListCategory)
    {
        IAlpacaDataSubscription<ITrade>? tradeSubscription = null;
        IAlpacaDataSubscription<IBar>? barSubscription = null;
        IAlpacaDataSubscription<IQuote>? quoteSubscription = null;

        foreach (var asset in assets)
        {
            if (Broker.Environment == TradingEnvironment.Live)
            {
                IStock? stock = LiveStockObjects.GetStock(asset.Symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, watchListCategory);
                }
            }

            if (Broker.Environment == TradingEnvironment.Paper)
            {
                IStock? stock = PaperStockObjects.GetStock(asset.Symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, watchListCategory);
                }
            }

            var stockObject = StockObjects.GetStock(asset.Symbol);
            if (stockObject == null)
            {
                stockObject = new Stock(broker, asset, watchListCategory);
            }
        }

        if (broker.subscribed)
        {

            try
            {
                for (int i = 0; i < assets.Where(x => x.Class == AssetClass.Crypto).Count(); i += maxSymbolsAtOnetime)
                {
                    var assetSubset = assets.Where(x => x.Class == AssetClass.Crypto).Skip(i).Take(maxSymbolsAtOnetime);
                    var symbols = assetSubset.Select(x => x.Symbol).ToList();

                    tradeSubscription = Broker.AlpacaCryptoStreamingClient.GetTradeSubscription(symbols);
                    tradeSubscription.Received += CryptoTradeSubscription_Received;
                    await Broker.AlpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription).ConfigureAwait(false);

                    quoteSubscription = Broker.AlpacaCryptoStreamingClient.GetQuoteSubscription(symbols);
                    quoteSubscription.Received += CryptoQuoteSubscription_Received;
                    await Broker.AlpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription).ConfigureAwait(false);

                    barSubscription = Broker.AlpacaCryptoStreamingClient.GetMinuteBarSubscription(symbols);
                    barSubscription.Received += CryptoMinAggrSubscription_Received;
                    await Broker.AlpacaCryptoStreamingClient.SubscribeAsync(barSubscription).ConfigureAwait(false);

                    IEnumerable<IStock> cryptoStocks = LiveStockObjects.GetStocks(AssetClass.Crypto, symbols);
                    foreach (Stock stock in cryptoStocks)
                    {
                        stock.subscribed = true;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            if (broker.Environment == TradingEnvironment.Live)
            {
                try
                {
                    for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += maxSymbolsAtOnetime)
                    {
                        var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(maxSymbolsAtOnetime);
                        var symbols = assetSubset.Select(x => x.Symbol).ToList();

                        tradeSubscription = broker.AlpacaDataStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += UsEquityLiveTradeSubscription_Received;
                        await broker.AlpacaDataStreamingClient.SubscribeAsync(tradeSubscription).ConfigureAwait(false);

                        quoteSubscription = broker.AlpacaDataStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += UsEquityLiveQuoteSubscription_Received;
                        await broker.AlpacaDataStreamingClient.SubscribeAsync(quoteSubscription).ConfigureAwait(false);

                        barSubscription = broker.AlpacaDataStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
                        await broker.AlpacaDataStreamingClient.SubscribeAsync(barSubscription).ConfigureAwait(false);

                        IEnumerable<IStock> usEquityStocks = LiveStockObjects.GetStocks(AssetClass.Crypto, symbols);
                        foreach (Stock stock in usEquityStocks)
                        {
                            stock.subscribed = true;
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }

            }
            if (broker.Environment == TradingEnvironment.Paper)
            {
                try
                {
                    for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += maxSymbolsAtOnetime)
                    {
                        var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(maxSymbolsAtOnetime);
                        var symbols = assetSubset.Select(x => x.Symbol).ToList();

                        tradeSubscription = broker.AlpacaDataStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += UsEquityPaperTradeSubscription_Received;
                        await broker.AlpacaDataStreamingClient.SubscribeAsync(tradeSubscription).ConfigureAwait(false);

                        quoteSubscription = broker.AlpacaDataStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += UsEquityPaperQuoteSubscription_Received;
                        await broker.AlpacaDataStreamingClient.SubscribeAsync(quoteSubscription).ConfigureAwait(false);

                        barSubscription = broker.AlpacaDataStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
                        await broker.AlpacaDataStreamingClient.SubscribeAsync(barSubscription).ConfigureAwait(false);

                        var usEquityStocks = PaperStockObjects.GetStocks(AssetClass.UsEquity, symbols);
                        foreach (var stock in usEquityStocks)
                        {
                            stock.subscribed = true;
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
        }
    }

    /// <summary>
    /// Subscribe Minute Bars for all sysbols in both environment
    /// </summary>
    /// <param name="liveBroker"></param>
    /// <param name="paperBroker"></param>
    /// <returns></returns>
    static public async Task SubscribeMinutesBarForAllSymbols(Broker liveBroker, Broker paperBroker)
    {
        if (!MinutesBarSubscribed)
        {
            IAlpacaDataSubscription<IBar>? minAggrSubscription = null;

            //only one environment 
            //Minute aggregated data for all crypto symbol
            minAggrSubscription = Broker.AlpacaCryptoStreamingClient.GetMinuteBarSubscription("*");
            minAggrSubscription.Received += CryptoMinAggrSubscription_Received;
            await Broker.AlpacaCryptoStreamingClient.SubscribeAsync(minAggrSubscription).ConfigureAwait(false);

            //live
            //Minute aggregated data for all usequity symbol
            minAggrSubscription = liveBroker.AlpacaDataStreamingClient.GetMinuteBarSubscription("*");
            minAggrSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
            await liveBroker.AlpacaDataStreamingClient.SubscribeAsync(minAggrSubscription).ConfigureAwait(false);

            //paper
            //Minute aggregated data for usequity symbol
            minAggrSubscription = paperBroker.AlpacaDataStreamingClient.GetMinuteBarSubscription("*");
            minAggrSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
            await paperBroker.AlpacaDataStreamingClient.SubscribeAsync(minAggrSubscription).ConfigureAwait(false);

            MinutesBarSubscribed = true;
        }
    }

    /// <summary>
    /// Generate events for all stock every one minute
    /// </summary>
    /// <param name="token"></param>
    static public async void GenerateEvents(int interval, CancellationToken token)
    {
        await Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (!Broker.subscribed)
                {
                    //get all snapshots (not used as quotes are subscribed)
                    await UpdateStocksWithSnapshots(TradingEnvironment.Paper).ConfigureAwait(false);
                    await UpdateStocksWithSnapshots(TradingEnvironment.Live).ConfigureAwait(false);
                }
                //update and raise event for GUI
                GenerateStockUpdatedEvent(TradingEnvironment.Paper);
                GenerateStockUpdatedEvent(TradingEnvironment.Live);

                //delay for the UnScibscribedRefreshInterval 
                await Task.Delay(TimeSpan.FromSeconds(interval), token).ConfigureAwait(false);
            }
        }, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Update Stock with snapshots (non sucscribed)
    /// </summary>
    /// <param name="environment"></param>
    /// <param name="assetClass"></param>
    /// <param name="assets"></param>
    /// <returns></returns>
    static private async Task UpdateStocksWithSnapshots(TradingEnvironment environment)
    {
        IEnumerable<IAsset?>? assets = null;
        if (environment == TradingEnvironment.Live)
            assets = LiveStockObjects.GetAssets();
        if (environment == TradingEnvironment.Paper)
            assets = PaperStockObjects.GetAssets();
        assets = StockObjects.GetAssets();

        if (assets != null)
        {
            var symbolAndSnapshotList = await Broker.ListSnapShots(assets, 5000).ConfigureAwait(false);

            foreach (var symbolAndSnapshot in symbolAndSnapshotList)
            {
                IStock? stock = null;
                if (environment == TradingEnvironment.Live)
                    stock = LiveStockObjects.GetStock(symbolAndSnapshot.Key);
                if (environment == TradingEnvironment.Paper)
                    stock = PaperStockObjects.GetStock(symbolAndSnapshot.Key);
                stock = StockObjects.GetStock(symbolAndSnapshot.Key);

                if (stock != null)
                {
                    stock.Quote = symbolAndSnapshot.Value?.Quote;
                }
            }

            var symbolAndTradesList = await Broker.ListTrades(assets, 5000).ConfigureAwait(false);

            foreach (var symbolAndTrades in symbolAndTradesList)
            {
                IStock? stock = null;
                if (environment == TradingEnvironment.Live)
                    stock = LiveStockObjects.GetStock(symbolAndTrades.Key);
                if (environment == TradingEnvironment.Paper)
                    stock = PaperStockObjects.GetStock(symbolAndTrades.Key);
                stock = StockObjects.GetStock(symbolAndTrades.Key);
                if (stock != null)
                {
                    stock.Trade = symbolAndTrades.Value;
                }
            }
        }
    }

    #region UsEquity subscription receiving methods
    /// <summary>
    /// event handler to minute UsEquity live aggregation data from a streaming client
    /// </summary>
    /// <param name="obj"></param>
    static private void UsEquityLiveMinAggrSubscription_Received(IBar obj)
    {
        IStock? stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.MinuteBar = obj;
        }
    }

    /// <summary>
    /// event handler to receive UsEquity live trade related data in the market
    /// this get the last price of asset
    /// </summary>
    /// <param name="obj"></param>
    static private void UsEquityLiveTradeSubscription_Received(ITrade obj)
    {
        IStock? stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Trade = obj;
        }
    }

    /// <summary>
    /// event handler to receive UsEquity live quote related data in the market
    /// this get the last price of asset
    /// </summary>
    /// <param name="obj"></param>
    static private void UsEquityLiveQuoteSubscription_Received(IQuote obj)
    {
        IStock? stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Quote = obj;
        }
    }

    /// <summary>
    /// event handler to UsEquity paper minute aggregation data from a streaming client
    /// </summary>
    /// <param name="obj"></param>
    static private void UsEquityPaperMinAggrSubscription_Received(IBar obj)
    {
        IStock? stock = PaperStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.MinuteBar = obj;
        }
    }

    /// <summary>
    /// event handler to receive UsEquity paper trade related data in the market
    /// this get the last price of asset
    /// </summary>
    /// <param name="obj"></param>
    static private void UsEquityPaperTradeSubscription_Received(ITrade obj)
    {
        IStock? stock = PaperStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Trade = obj;
        }
    }

    /// <summary>
    /// event handler to receive UsEquity paper quote related data in the market
    /// this get the last price of asset
    /// </summary>
    /// <param name="obj"></param>
    static private void UsEquityPaperQuoteSubscription_Received(IQuote obj)
    {
        IStock? stock = PaperStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Quote = obj;
        }
    }
    #endregion

    #region crypto subscription receiving methods
    /// <summary>
    /// event handler to crypto paper minute aggregation data from a streaming client
    /// </summary>
    /// <param name="obj"></param>
    static private void CryptoMinAggrSubscription_Received(IBar obj)
    {
        IStock? stock = null;
        stock = PaperStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.MinuteBar = obj;
        }
        stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.MinuteBar = obj;
        }
    }

    /// <summary>
    /// event handler to receive crypto paper trade related data in the market
    /// this get the last price of asset
    /// </summary>
    /// <param name="obj"></param>
    static private void CryptoTradeSubscription_Received(ITrade obj)
    {
        IStock? stock = null;
        stock = PaperStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Trade = obj;
        }
        stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Trade = obj;
        }
    }

    /// <summary>
    /// event handler to receive crypto paper quote related data in the market
    /// this get the last price of asset
    /// </summary>
    /// <param name="obj"></param>
    static private void CryptoQuoteSubscription_Received(IQuote obj)
    {
        IStock? stock = null;
        stock = PaperStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Quote = obj;
        }
        stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Quote = obj;
        }
    }
    #endregion

    /// <summary>
    /// Evant handler to send stockList
    /// </summary>
    static public event EventHandler PaperStockUpdated = default!;
    static public event EventHandler LiveStockUpdated = default!;
    static public event EventHandler StockUpdated = default!;

    /// <summary>
    /// Invoke paper stock updated event
    /// </summary>
    /// <param name="e"></param>
    static protected void OnPaperStockUpdatedEvent(EventArgs e)
    {
        PaperStockUpdated(null, e);
    }

    /// <summary>
    /// Invoke live stock updated event
    /// </summary>
    /// <param name="e"></param>
    static protected void OnLiveStockUpdatedEvent(EventArgs e)
    {
        LiveStockUpdated(null, e);
    }

    /// <summary>
    /// Invoke stock updated event
    /// </summary>
    /// <param name="e"></param>
    static protected void OnStockUpdatedEvent(EventArgs e)
    {
        StockUpdated(null, e);
    }

    /// <summary>
    /// Generate send all stock data to ui
    /// </summary>
    /// <returns></returns>
    static public void GenerateStockUpdatedEvent(TradingEnvironment environment)
    {
        try
        {
            IEnumerable<IStock>? stockObjects = null;
            if (environment == TradingEnvironment.Live)
            {
                stockObjects = LiveStockObjects.GetStocks();
                StockUpdatedEventArgs suea = new()
                {
                    Stocks = stockObjects.ToList()
                };
                OnLiveStockUpdatedEvent(suea);
            }
            if (environment == TradingEnvironment.Paper)
            {
                stockObjects = PaperStockObjects.GetStocks();
                StockUpdatedEventArgs suea = new()
                {
                    Stocks = stockObjects.ToList()
                };
                OnPaperStockUpdatedEvent(suea);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
