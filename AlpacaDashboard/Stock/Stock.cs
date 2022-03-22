namespace AlpacaDashboard;

public class Stock : IStock
{
    public static bool MinutesBarSubscribed = false;
    public static Broker Broker { get; set; } = default!;
    public static StockList PaperStockObjects = new();
    public static StockList LiveStockObjects = new();
    public IAsset Asset { get; set; }
    public object? Tag { get; set; }
    public string? Symbol { get; set; }
    public bool subscribed { get; set; }
    public decimal? Qty { get; set; }
    public decimal? Last { get; set; }
    public decimal? Low { get; set; }
    public decimal? High { get; set; }
    public decimal? Volume { get; set; }
    public decimal? Vwap { get; set; }
    public decimal? Open { get; set; }
    public decimal? Close { get; set; }
    public decimal? MinuteBarClose { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? OpenPositionValue { get; set; }
    public decimal? BidPrice { get; set; }
    public decimal? BidSize { get; set; }
    public string? BidExchange { get; set; }
    public decimal? AskPrice { get; set; }
    public decimal? AskSize { get; set; }
    public string? AskExchange { get; set; }
    public Broker broker { get; set; }
    public DateTime? MinuteBarDateTime { get; set; }
    public DateTime? QuoteDateTime { get; set; }
    public DateTime? TradeDateTime { get; set; }

    public Stock(Broker broker, IAsset asset, string symbol, string type)
    {
        this.broker = broker;
        this.Symbol = symbol;
        this.Asset = asset;
        this.Tag = (string)type;

        IStock? stock = null;
        if (broker.Environment == "Live")
        {
            stock = LiveStockObjects.GetStock(symbol);
            if (stock == null)
            {
                LiveStockObjects.Add(this);
            }
        }
        if (broker.Environment == "Paper" && Stock.PaperStockObjects != null)
        {
            stock = PaperStockObjects.GetStock(symbol);
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
        if (asset != null)
        {
            if (broker.Environment == "Live")
            {
                IStock? stock = Stock.LiveStockObjects.GetStock(symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, symbol, watchListCategory);
                }
            }
            if (broker.Environment == "Paper")
            {
                IStock? stock = Stock.PaperStockObjects.GetStock(symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, symbol, watchListCategory);
                }
            }

            if (broker.subscribed)
            {

                if (asset.Class == AssetClass.Crypto)
                {
                    tradeSubscription = Broker.alpacaCryptoStreamingClient.GetTradeSubscription(symbol);
                    tradeSubscription.Received += CryptoTradeSubscription_Received;
                    await Broker.alpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription);

                    quoteSubscription = Broker.alpacaCryptoStreamingClient.GetQuoteSubscription(symbol);
                    quoteSubscription.Received += CryptoQuoteSubscription_Received;
                    await Broker.alpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription);

                    barSubscription = Broker.alpacaCryptoStreamingClient.GetMinuteBarSubscription(symbol);
                    barSubscription.Received += CryptoMinAggrSubscription_Received;
                    await Broker.alpacaCryptoStreamingClient.SubscribeAsync(barSubscription);
                }

                if (broker.Environment == "Live")
                {
                    IStock? stock = LiveStockObjects.GetStock(symbol);
                    if (asset.Class == AssetClass.UsEquity)
                    {
                        tradeSubscription = broker.alpacaDataStreamingClient.GetTradeSubscription(symbol);
                        tradeSubscription.Received += UsEquityLiveTradeSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(tradeSubscription);

                        quoteSubscription = broker.alpacaDataStreamingClient.GetQuoteSubscription(symbol);
                        quoteSubscription.Received += UsEquityLiveQuoteSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(quoteSubscription);

                        barSubscription = broker.alpacaDataStreamingClient.GetMinuteBarSubscription(symbol);
                        barSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(barSubscription);
                    }

                    if (stock != null)
                        stock.subscribed = true;
                }
                if (broker.Environment == "Paper")
                {
                    IStock? stock = PaperStockObjects.GetStock(symbol);
                    if (asset.Class == AssetClass.UsEquity)
                    {
                        tradeSubscription = broker.alpacaDataStreamingClient.GetTradeSubscription(symbol);
                        tradeSubscription.Received += UsEquityPaperTradeSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(tradeSubscription);

                        quoteSubscription = broker.alpacaDataStreamingClient.GetQuoteSubscription(symbol);
                        quoteSubscription.Received += UsEquityPaperQuoteSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(quoteSubscription);

                        barSubscription = broker.alpacaDataStreamingClient.GetMinuteBarSubscription(symbol);
                        barSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(barSubscription);
                    }

                    if (stock != null)
                        stock.subscribed = true;
                }
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

        foreach (IAsset asset in assets)
        {
            if (Broker.Environment == "Live")
            {
                IStock? stock = LiveStockObjects.GetStock(asset.Symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, asset.Symbol, watchListCategory);
                }
            }
            if (Broker.Environment == "Paper")
            {
                IStock? stock = PaperStockObjects.GetStock(asset.Symbol);
                if (stock == null)
                {
                    stock = new Stock(broker, asset, asset.Symbol, watchListCategory);
                }
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

                    tradeSubscription = Broker.alpacaCryptoStreamingClient.GetTradeSubscription(symbols);
                    tradeSubscription.Received += CryptoTradeSubscription_Received;
                    await Broker.alpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription).ConfigureAwait(false);

                    quoteSubscription = Broker.alpacaCryptoStreamingClient.GetQuoteSubscription(symbols);
                    quoteSubscription.Received += CryptoQuoteSubscription_Received;
                    await Broker.alpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription).ConfigureAwait(false);

                    barSubscription = Broker.alpacaCryptoStreamingClient.GetMinuteBarSubscription(symbols);
                    barSubscription.Received += CryptoMinAggrSubscription_Received;
                    await Broker.alpacaCryptoStreamingClient.SubscribeAsync(barSubscription).ConfigureAwait(false);
                    
                    IEnumerable<IStock> cryptoStocks = LiveStockObjects.GetStocks(AssetClass.Crypto, symbols);
                    foreach (Stock stock in cryptoStocks)
                    {
                        stock.subscribed = true;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            if (broker.Environment == "Live")
            {
                try
                {
                    for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += maxSymbolsAtOnetime)
                    {
                        var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(maxSymbolsAtOnetime);
                        var symbols = assetSubset.Select(x => x.Symbol).ToList();

                        tradeSubscription = broker.alpacaDataStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += UsEquityLiveTradeSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(tradeSubscription).ConfigureAwait(false);

                        quoteSubscription = broker.alpacaDataStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += UsEquityLiveQuoteSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(quoteSubscription).ConfigureAwait(false);

                        barSubscription = broker.alpacaDataStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(barSubscription).ConfigureAwait(false);

                        IEnumerable<IStock> usEquityStocks = LiveStockObjects.GetStocks(AssetClass.Crypto, symbols);
                        foreach (Stock stock in usEquityStocks)
                        {
                            stock.subscribed = true;
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }

            }
            if (broker.Environment == "Paper")
            {
                try
                {
                    for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += maxSymbolsAtOnetime)
                    {
                        var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(maxSymbolsAtOnetime);
                        var symbols = assetSubset.Select(x => x.Symbol).ToList();

                        tradeSubscription = broker.alpacaDataStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += UsEquityPaperTradeSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(tradeSubscription).ConfigureAwait(false);

                        quoteSubscription = broker.alpacaDataStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += UsEquityPaperQuoteSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(quoteSubscription).ConfigureAwait(false);

                        barSubscription = broker.alpacaDataStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(barSubscription).ConfigureAwait(false);

                        IEnumerable<IStock> usEquityStocks = PaperStockObjects.GetStocks(AssetClass.UsEquity, symbols);
                        foreach (Stock stock in usEquityStocks)
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
            minAggrSubscription = Broker.alpacaCryptoStreamingClient.GetMinuteBarSubscription("*");
            minAggrSubscription.Received += CryptoMinAggrSubscription_Received;
            await Broker.alpacaCryptoStreamingClient.SubscribeAsync(minAggrSubscription).ConfigureAwait(false);

            //live
            //Minute aggregated data for all usequity symbol
            minAggrSubscription = liveBroker.alpacaDataStreamingClient.GetMinuteBarSubscription("*");
            minAggrSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
            await liveBroker.alpacaDataStreamingClient.SubscribeAsync(minAggrSubscription).ConfigureAwait(false);

            //paper
            //Minute aggregated data for usequity symbol
            minAggrSubscription = paperBroker.alpacaDataStreamingClient.GetMinuteBarSubscription("*");
            minAggrSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
            await paperBroker.alpacaDataStreamingClient.SubscribeAsync(minAggrSubscription).ConfigureAwait(false);

            MinutesBarSubscribed = true;
        }
    }

    /// <summary>
    /// Generate events for all stock every one minute
    /// </summary>
    /// <param name="token"></param>
    static public async void GenerateEvents(string environment, int interval, CancellationToken token)
    {
        await Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (!Broker.subscribed)
                { 
                    //get all snapshots (not used as quotes are subscribed)
                    await UpdateStocksWithSnapshots("Paper").ConfigureAwait(false);
                    await UpdateStocksWithSnapshots("Live").ConfigureAwait(false);
                }
                //update and raise event for GUI
                GenerateStockUpdatedEvent("Paper");
                GenerateStockUpdatedEvent("Live");

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
    static private async Task UpdateStocksWithSnapshots(string environment)
    {
        IEnumerable<IAsset>? assets = null;
        if (environment == "Live")
            assets = LiveStockObjects.GetAssets();
        if (environment == "Paper")
            assets = PaperStockObjects.GetAssets();

        if (assets != null)
        {
            var symbolAndSnapshotList = await Broker.ListSnapShots(assets, 5000).ConfigureAwait(false);
            foreach (var symbolAndSnapshot in symbolAndSnapshotList)
            {
                IStock? stock = null;
                if (environment == "Live")
                    stock = LiveStockObjects.GetStock(symbolAndSnapshot.Key);
                if (environment == "Paper")
                    stock = PaperStockObjects.GetStock(symbolAndSnapshot.Key);
                if (stock != null)
                {
                    stock.BidSize = symbolAndSnapshot.Value?.Quote?.BidSize;
                    stock.AskSize = symbolAndSnapshot.Value?.Quote?.AskSize;
                    stock.BidPrice = symbolAndSnapshot.Value?.Quote?.BidPrice;
                    stock.AskPrice = symbolAndSnapshot.Value?.Quote?.AskPrice;
                    stock.BidExchange = symbolAndSnapshot.Value?.Quote?.BidExchange;
                    stock.AskExchange = symbolAndSnapshot.Value?.Quote?.AskExchange;
                    stock.QuoteDateTime = symbolAndSnapshot.Value?.Quote?.TimestampUtc;
                }
            }

            var symbolAndTradesList = await Broker.ListTrades(assets, 5000).ConfigureAwait(false);
            foreach (var symbolAndTrades in symbolAndTradesList)
            {
                IStock? stock = null;
                if (environment == "Live")
                    stock = LiveStockObjects.GetStock(symbolAndTrades.Key);
                if (environment == "Paper")
                    stock = PaperStockObjects.GetStock(symbolAndTrades.Key);
                if (stock != null)
                {
                    stock.Last = symbolAndTrades.Value.Price;
                    stock.TradeDateTime = symbolAndTrades.Value.TimestampUtc;
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
            stock.Open = obj.Open;
            stock.Close = obj.Close;
            stock.High = obj.High;
            stock.Low = obj.Low;
            stock.Vwap = obj.Vwap;
            stock.Volume = obj.Volume;
            stock.Last = obj.Close;
            stock.MinuteBarClose = obj.Close;
            stock.MinuteBarDateTime = obj.TimeUtc;
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
            stock.Last = obj.Price;
            stock.TradeDateTime = obj.TimestampUtc;
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
            stock.AskExchange = obj.AskExchange;
            stock.AskSize = obj.AskSize;
            stock.AskPrice = obj.AskPrice;
            stock.BidExchange = obj.BidExchange;
            stock.BidSize = obj.BidSize;
            stock.BidPrice = obj.BidPrice;
            stock.QuoteDateTime = obj.TimestampUtc;
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
            stock.Open = obj.Open;
            stock.Close = obj.Close;
            stock.High = obj.High;
            stock.Low = obj.Low;
            stock.Vwap = obj.Vwap;
            stock.Volume = obj.Volume;
            stock.Last = obj.Close;
            stock.MinuteBarClose = obj.Close;
            stock.MinuteBarDateTime = obj.TimeUtc;
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
            stock.Last = obj.Price;
            stock.TradeDateTime = obj.TimestampUtc;
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
            stock.AskExchange = obj.AskExchange;
            stock.AskSize = obj.AskSize;
            stock.AskPrice = obj.AskPrice;
            stock.BidExchange = obj.BidExchange;
            stock.BidSize = obj.BidSize;
            stock.BidPrice = obj.BidPrice;
            stock.QuoteDateTime = obj.TimestampUtc;
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
            stock.Open = obj.Open;
            stock.Close = obj.Close;
            stock.High = obj.High;
            stock.Low = obj.Low;
            stock.Vwap = obj.Vwap;
            stock.Volume = obj.Volume;
            stock.Last = obj.Close;
            stock.MinuteBarClose = obj.Close;
            stock.MinuteBarDateTime = obj.TimeUtc;
        }
        stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Open = obj.Open;
            stock.Close = obj.Close;
            stock.High = obj.High;
            stock.Low = obj.Low;
            stock.Vwap = obj.Vwap;
            stock.Volume = obj.Volume;
            stock.Last = obj.Close;
            stock.MinuteBarClose = obj.Close;
            stock.MinuteBarDateTime = obj.TimeUtc;
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
            stock.Last = obj.Price;
            stock.TradeDateTime = obj.TimestampUtc;
        }
        stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.Last = obj.Price;
            stock.TradeDateTime = obj.TimestampUtc;
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
            stock.AskExchange = obj.AskExchange;
            stock.AskSize = obj.AskSize;
            stock.AskPrice = obj.AskPrice;
            stock.BidExchange = obj.BidExchange;
            stock.BidSize = obj.BidSize;
            stock.BidPrice = obj.BidPrice;
            stock.QuoteDateTime = obj.TimestampUtc;
        }
        stock = LiveStockObjects.GetStock(obj.Symbol);
        if (stock != null)
        {
            stock.AskExchange = obj.AskExchange;
            stock.AskSize = obj.AskSize;
            stock.AskPrice = obj.AskPrice;
            stock.BidExchange = obj.BidExchange;
            stock.BidSize = obj.BidSize;
            stock.BidPrice = obj.BidPrice;
            stock.QuoteDateTime = obj.TimestampUtc;
        }
    }
    #endregion

    /// <summary>
    /// Evant handler to send stockList
    /// </summary>
    static public event EventHandler PaperStockUpdated = default!;
    static public event EventHandler LiveStockUpdated = default!;

    /// <summary>
    /// Invoke stok updated event
    /// </summary>
    /// <param name="e"></param>
    static protected void OnPaperStockUpdatedEvent(EventArgs e)
    {
        PaperStockUpdated(null, e);
    }

    /// <summary>
    /// Invoke stok updated event
    /// </summary>
    /// <param name="e"></param>
    static protected void OnLiveStockUpdatedEvent(EventArgs e)
    {
        LiveStockUpdated(null, e);
    }

    /// <summary>
    /// Generate send all stock data to ui
    /// </summary>
    /// <returns></returns>
    static public void GenerateStockUpdatedEvent(string environment)
    {
        try
        {
            IEnumerable<IStock>? stockObjects = null;
            if (environment == "Live")
            {
                stockObjects = LiveStockObjects.GetStocks();
                StockUpdatedEventArgs suea = new()
                {
                    Stocks = stockObjects.ToList()
                };
                OnLiveStockUpdatedEvent(suea);
            }
            if (environment == "Paper")
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
