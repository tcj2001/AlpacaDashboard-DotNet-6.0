using Alpaca.Markets;
using Alpaca.Markets.Extensions;
using AlpacaDashboard.Brokers;

namespace AlpacaDashboard
{
    public class Stock : IStock
    {
        public static bool MinutesBarSubscribed = false;
        public static Broker Broker { get; set; }
        public static StockList PaperStockObjects = new StockList();
        public static StockList LiveStockObjects = new StockList();
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
        public decimal? MarketValue { get; set; }
        public decimal? OpenPositionValue { get; set; }
        public decimal? BidPrice { get; set; }
        public decimal? BidSize { get; set; }
        public string? BidExchange { get; set; }
        public decimal? AskPrice { get; set; }
        public decimal? AskSize { get; set; }
        public string? AskExchange { get; set; }
        public Broker broker { get; set; }

        public Stock(Broker broker, IAsset asset, string symbol, string type)
        {
            this.broker = broker;
            this.Symbol = symbol;
            this.Asset = asset;
            this.Tag = (string)type;

            IStock stock = null;
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
            IAlpacaDataSubscription<ITrade> tradeSubscription = null;
            IAlpacaDataSubscription<IBar> barSubscription = null;
            IAlpacaDataSubscription<IQuote> quoteSubscription = null;

            IAsset asset = await broker.GetAsset(symbol);
            if (asset != null)
            {
                if (broker.Environment == "Live")
                {
                    IStock stock = Stock.LiveStockObjects.GetStock(symbol);
                    if (stock == null)
                    {
                        stock = new Stock(broker, asset, symbol, watchListCategory);
                    }
                }
                if (broker.Environment == "Paper")
                {
                    IStock stock = Stock.PaperStockObjects.GetStock(symbol);
                    if (stock == null)
                    {
                        stock = new Stock(broker, asset, symbol, watchListCategory);
                    }
                }

                if (broker.subscribed)
                {

                    if (broker.Environment == "Live")
                    {
                        IStock stock = LiveStockObjects.GetStock(symbol);
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

                        if (asset.Class == AssetClass.Crypto)
                        {
                            tradeSubscription = broker.alpacaCryptoStreamingClient.GetTradeSubscription(symbol);
                            tradeSubscription.Received += CryptoLiveTradeSubscription_Received;
                            await broker.alpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription);

                            quoteSubscription = broker.alpacaCryptoStreamingClient.GetQuoteSubscription(symbol);
                            quoteSubscription.Received += CryptoLiveQuoteSubscription_Received;
                            await broker.alpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription);

                            barSubscription = broker.alpacaCryptoStreamingClient.GetMinuteBarSubscription(symbol);
                            barSubscription.Received += CryptoLiveMinAggrSubscription_Received;
                            await broker.alpacaCryptoStreamingClient.SubscribeAsync(barSubscription);
                        }
                        if (stock != null)
                            stock.subscribed = true;
                    }
                    if (broker.Environment == "Paper")
                    {
                        IStock stock = PaperStockObjects.GetStock(symbol);
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

                        if (asset.Class == AssetClass.Crypto)
                        {
                            tradeSubscription = broker.alpacaCryptoStreamingClient.GetTradeSubscription(symbol);
                            tradeSubscription.Received += CryptoPaperTradeSubscription_Received;
                            await broker.alpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription);

                            quoteSubscription = broker.alpacaCryptoStreamingClient.GetQuoteSubscription(symbol);
                            quoteSubscription.Received += CryptoPaperQuoteSubscription_Received;
                            await broker.alpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription);

                            barSubscription = broker.alpacaCryptoStreamingClient.GetMinuteBarSubscription(symbol);
                            barSubscription.Received += CryptoPaperMinAggrSubscription_Received;
                            await broker.alpacaCryptoStreamingClient.SubscribeAsync(barSubscription);
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
        static public async Task Subscribe(Broker broker, IEnumerable<string> symbols, string watchListCategory)
        {
            IAlpacaDataSubscription<ITrade> tradeSubscription = null;
            IAlpacaDataSubscription<IBar> barSubscription = null;
            IAlpacaDataSubscription<IQuote> quoteSubscription = null;

            foreach (string symbol in symbols)
            {
                IAsset asset = await broker.GetAsset(symbol);
                if (Broker.Environment == "Live")
                {
                    IStock stock = Stock.LiveStockObjects.GetStock(symbol);
                    if (stock == null)
                    {
                        stock = new Stock(broker, asset, symbol, watchListCategory);
                    }
                }
                if (Broker.Environment == "Paper")
                {
                    IStock stock = Stock.PaperStockObjects.GetStock(symbol);
                    if (stock == null)
                    {
                        stock = new Stock(broker, asset, symbol, watchListCategory);
                    }
                }
            }

            if (broker.subscribed)
            {

                if (broker.Environment == "Live")
                {
                    try
                    {
                        IEnumerable<IStock> usEquityStocks = LiveStockObjects.GetStocks(AssetClass.UsEquity, symbols);
                        tradeSubscription = broker.alpacaDataStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += UsEquityLiveTradeSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(tradeSubscription);

                        quoteSubscription = broker.alpacaDataStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += UsEquityLiveQuoteSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(quoteSubscription);

                        barSubscription = broker.alpacaDataStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(barSubscription);
                        foreach (Stock stock in usEquityStocks)
                        {
                            stock.subscribed = true;
                        }
                    }
                    catch (Exception ex) { }


                    try
                    {
                        IEnumerable<IStock> cryptoStocks = LiveStockObjects.GetStocks(AssetClass.Crypto, symbols);
                        tradeSubscription = broker.alpacaCryptoStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += CryptoLiveTradeSubscription_Received;
                        await broker.alpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription);

                        quoteSubscription = broker.alpacaCryptoStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += CryptoLiveQuoteSubscription_Received;
                        await broker.alpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription);

                        barSubscription = broker.alpacaCryptoStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += CryptoLiveMinAggrSubscription_Received;
                        await broker.alpacaCryptoStreamingClient.SubscribeAsync(barSubscription);
                        foreach (Stock stock in cryptoStocks)
                        {
                            stock.subscribed = true;
                        }
                    }
                    catch (Exception ex) { }

                }
                if (broker.Environment == "Paper")
                {
                    try
                    {
                        IEnumerable<IStock> usEquityStocks = PaperStockObjects.GetStocks(AssetClass.UsEquity, symbols);
                        tradeSubscription = broker.alpacaDataStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += UsEquityPaperTradeSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(tradeSubscription);

                        quoteSubscription = broker.alpacaDataStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += UsEquityPaperQuoteSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(quoteSubscription);

                        barSubscription = broker.alpacaDataStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
                        await broker.alpacaDataStreamingClient.SubscribeAsync(barSubscription);
                        foreach (Stock stock in usEquityStocks)
                        {
                            stock.subscribed = true;
                        }
                    }
                    catch (Exception ex) { }

                    try
                    {
                        IEnumerable<IStock> cryptoStocks = PaperStockObjects.GetStocks(AssetClass.Crypto, symbols);
                        tradeSubscription = broker.alpacaCryptoStreamingClient.GetTradeSubscription(symbols);
                        tradeSubscription.Received += CryptoPaperTradeSubscription_Received;
                        await broker.alpacaCryptoStreamingClient.SubscribeAsync(tradeSubscription);

                        quoteSubscription = broker.alpacaCryptoStreamingClient.GetQuoteSubscription(symbols);
                        quoteSubscription.Received += CryptoPaperQuoteSubscription_Received;
                        await broker.alpacaCryptoStreamingClient.SubscribeAsync(quoteSubscription);

                        barSubscription = broker.alpacaCryptoStreamingClient.GetMinuteBarSubscription(symbols);
                        barSubscription.Received += CryptoPaperMinAggrSubscription_Received;
                        await broker.alpacaCryptoStreamingClient.SubscribeAsync(barSubscription);
                        foreach (Stock stock in cryptoStocks)
                        {
                            stock.subscribed = true;
                        }
                    }
                    catch (Exception ex) { }
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
                IAlpacaDataSubscription<IBar> minAggrSubscription = null;

                //live
                //Minute aggregated data for all usequity symbol
                minAggrSubscription = liveBroker.alpacaDataStreamingClient.GetMinuteBarSubscription("*");
                minAggrSubscription.Received += UsEquityLiveMinAggrSubscription_Received;
                await liveBroker.alpacaDataStreamingClient.SubscribeAsync(minAggrSubscription);
                //Minute aggregated data for all crypto symbol
                minAggrSubscription = liveBroker.alpacaCryptoStreamingClient.GetMinuteBarSubscription("*");
                minAggrSubscription.Received += CryptoLiveMinAggrSubscription_Received;
                await liveBroker.alpacaCryptoStreamingClient.SubscribeAsync(minAggrSubscription);

                //paper
                //Minute aggregated data for usequity symbol
                minAggrSubscription = paperBroker.alpacaDataStreamingClient.GetMinuteBarSubscription("*");
                minAggrSubscription.Received += UsEquityPaperMinAggrSubscription_Received;
                await paperBroker.alpacaDataStreamingClient.SubscribeAsync(minAggrSubscription);
                //Minute aggregated data for crypto symbol
                minAggrSubscription = paperBroker.alpacaCryptoStreamingClient.GetMinuteBarSubscription("*");
                minAggrSubscription.Received += CryptoPaperMinAggrSubscription_Received;
                await paperBroker.alpacaCryptoStreamingClient.SubscribeAsync(minAggrSubscription);

                MinutesBarSubscribed = true;
            }
        }

        /// <summary>
        /// Generate events for all stock every one minute
        /// </summary>
        /// <param name="token"></param>
        static public async void GenerateEvents(string environment, int interval, CancellationToken token)
        {
            Task t = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (!Broker.subscribed)
                    { 
                        //get all snapshots (not used as quotes are subscribed)
                        await UpdateStocksWithSnapshots("Paper");
                        await UpdateStocksWithSnapshots("Live");
                    }
                    //update and raise event for GUI
                    await GenerateStockUpdatedEvent("Paper");
                    await GenerateStockUpdatedEvent("Live");

                    //delay for the UnScibscribedRefreshInterval 
                    await Task.Delay(TimeSpan.FromSeconds(interval), token);
                }
            }, token);
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
            IEnumerable<IAsset> assets = null;
            if (environment == "Live")
                assets = LiveStockObjects.GetAssets();
            if (environment == "Paper")
                assets = PaperStockObjects.GetAssets();
            var symbolAndSnapshotList = await Broker.ListSnapShots(assets, 5000);
            foreach (var symbolAndSnapshot in symbolAndSnapshotList)
            {
                IStock stock = null;
                if (environment == "Live")
                    stock = LiveStockObjects.GetStock(symbolAndSnapshot.Key);
                if (environment == "Paper")
                    stock = PaperStockObjects.GetStock(symbolAndSnapshot.Key);
                if (stock != null)
                {
                    stock.BidSize = symbolAndSnapshot.Value.Quote.BidSize;
                    stock.AskSize = symbolAndSnapshot.Value.Quote.AskSize;
                    stock.BidPrice = symbolAndSnapshot.Value.Quote.BidPrice;
                    stock.AskPrice = symbolAndSnapshot.Value.Quote.AskPrice;
                    stock.BidExchange = symbolAndSnapshot.Value.Quote.BidExchange;
                    stock.AskExchange = symbolAndSnapshot.Value.Quote.AskExchange;
                }
            }
            var symbolAndTradesList = await Broker.ListTrades(assets, 5000);
            foreach (var symbolAndTrades in symbolAndTradesList)
            {
                IStock stock = null;
                if (environment == "Live")
                    stock = LiveStockObjects.GetStock(symbolAndTrades.Key);
                if (environment == "Paper")
                    stock = PaperStockObjects.GetStock(symbolAndTrades.Key);
                if (stock != null)
                {
                    stock.Last = symbolAndTrades.Value.Price;
                }
            }
        }

        #region UsEquity subscription receiving methods
        /// <summary>
        /// event handler to minute UsEquity live aggregation data from a streaming client
        /// </summary>
        /// <param name="obj"></param>
        static private async void UsEquityLiveMinAggrSubscription_Received(IBar obj)
        {
            IStock stock = LiveStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Open = obj.Open;
                stock.Close = obj.Close;
                stock.High = obj.High;
                stock.Low = obj.Low;
                stock.Vwap = obj.Vwap;
                stock.Volume = obj.Volume;
                stock.Last = obj.Close;
            }
        }

        /// <summary>
        /// event handler to receive UsEquity live trade related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void UsEquityLiveTradeSubscription_Received(ITrade obj)
        {
            IStock stock = LiveStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Last = obj.Price;
            }
        }

        /// <summary>
        /// event handler to receive UsEquity live quote related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void UsEquityLiveQuoteSubscription_Received(IQuote obj)
        {
            IStock stock = LiveStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.AskExchange = obj.AskExchange;
                stock.AskSize = obj.AskSize;
                stock.AskPrice = obj.AskPrice;
                stock.BidExchange = obj.BidExchange;
                stock.BidSize = obj.BidSize;
                stock.BidPrice = obj.BidPrice;
            }
        }

        /// <summary>
        /// event handler to UsEquity paper minute aggregation data from a streaming client
        /// </summary>
        /// <param name="obj"></param>
        static private async void UsEquityPaperMinAggrSubscription_Received(IBar obj)
        {
            IStock stock = PaperStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Open = obj.Open;
                stock.Close = obj.Close;
                stock.High = obj.High;
                stock.Low = obj.Low;
                stock.Vwap = obj.Vwap;
                stock.Volume = obj.Volume;
                stock.Last = obj.Close;
            }
        }

        /// <summary>
        /// event handler to receive UsEquity paper trade related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void UsEquityPaperTradeSubscription_Received(ITrade obj)
        {
            IStock stock = PaperStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Last = obj.Price;
            }
        }

        /// <summary>
        /// event handler to receive UsEquity paper quote related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void UsEquityPaperQuoteSubscription_Received(IQuote obj)
        {
            IStock stock = PaperStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.AskExchange = obj.AskExchange;
                stock.AskSize = obj.AskSize;
                stock.AskPrice = obj.AskPrice;
                stock.BidExchange = obj.BidExchange;
                stock.BidSize = obj.BidSize;
                stock.BidPrice = obj.BidPrice;
            }
        }
        #endregion

        #region crypto subscription receiving methods
        /// <summary>
        /// event handler to minute crypto live aggregation data from a streaming client
        /// </summary>
        /// <param name="obj"></param>
        static private async void CryptoLiveMinAggrSubscription_Received(IBar obj)
        {
            IStock stock = LiveStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Open = obj.Open;
                stock.Close = obj.Close;
                stock.High = obj.High;
                stock.Low = obj.Low;
                stock.Vwap = obj.Vwap;
                stock.Volume = obj.Volume;
                stock.Last = obj.Close;
            }
        }

        /// <summary>
        /// event handler to receive crypto live trade related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void CryptoLiveTradeSubscription_Received(ITrade obj)
        {
            IStock stock = LiveStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Last = obj.Price;
            }
        }

        /// <summary>
        /// event handler to receive crypto live quote related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void CryptoLiveQuoteSubscription_Received(IQuote obj)
        {
            IStock stock = LiveStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.AskExchange = obj.AskExchange;
                stock.AskSize = obj.AskSize;
                stock.AskPrice = obj.AskPrice;
                stock.BidExchange = obj.BidExchange;
                stock.BidSize = obj.BidSize;
                stock.BidPrice = obj.BidPrice;
            }
        }

        /// <summary>
        /// event handler to crypto paper minute aggregation data from a streaming client
        /// </summary>
        /// <param name="obj"></param>
        static private async void CryptoPaperMinAggrSubscription_Received(IBar obj)
        {
            IStock stock = PaperStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Open = obj.Open;
                stock.Close = obj.Close;
                stock.High = obj.High;
                stock.Low = obj.Low;
                stock.Vwap = obj.Vwap;
                stock.Volume = obj.Volume;
                stock.Last = obj.Close;
            }
        }

        /// <summary>
        /// event handler to receive crypto paper trade related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void CryptoPaperTradeSubscription_Received(ITrade obj)
        {
            IStock stock = PaperStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.Last = obj.Price;
            }
        }

        /// <summary>
        /// event handler to receive crypto paper quote related data in the market
        /// this get the last price of asset
        /// </summary>
        /// <param name="obj"></param>
        static private void CryptoPaperQuoteSubscription_Received(IQuote obj)
        {
            IStock stock = PaperStockObjects.GetStock(obj.Symbol);
            if (stock != null)
            {
                stock.AskExchange = obj.AskExchange;
                stock.AskSize = obj.AskSize;
                stock.AskPrice = obj.AskPrice;
                stock.BidExchange = obj.BidExchange;
                stock.BidSize = obj.BidSize;
                stock.BidPrice = obj.BidPrice;
            }
        }
        #endregion

        /// <summary>
        /// Evant handler to send stockList
        /// </summary>
        static public event EventHandler PaperStockUpdated;
        static public event EventHandler LiveStockUpdated;

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
        static public async Task GenerateStockUpdatedEvent(string environment)
        {
            try
            {
                IEnumerable<IStock> stockObjects = null;
                if (environment == "Live")
                {
                    stockObjects = LiveStockObjects.GetStocks();
                    StockUpdatedEventArgs suea = new StockUpdatedEventArgs
                    {
                        Stocks = stockObjects.ToList()
                    };
                    OnLiveStockUpdatedEvent(suea);
                }
                if (environment == "Paper")
                {
                    stockObjects = PaperStockObjects.GetStocks();
                    StockUpdatedEventArgs suea = new StockUpdatedEventArgs
                    {
                        Stocks = stockObjects.ToList()
                    };
                    OnPaperStockUpdatedEvent(suea);
                }
            }
            catch (Exception ex)
            {
            }
        }

    }

}
