using Alpaca.Markets;
using Alpaca.Markets.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace AlpacaDashboard.Brokers
{
    /// <summary>
    /// This class handles all request related Alpaca Market and Data api
    /// </summary>
    public class Broker : IDisposable
    {
        #region public and private properties
        private string key;
        private string secret;
        public bool subscribed;

        private IAlpacaTradingClient alpacaTradingClient;

        private IAlpacaDataClient alpacaDataClient;
        private IAlpacaCryptoDataClient alpacaCryptoDataClient;

        public IAlpacaDataStreamingClient alpacaDataStreamingClient;
        public IAlpacaCryptoStreamingClient alpacaCryptoStreamingClient;

        private IAlpacaStreamingClient alpacaStreamingClient;

        private SecretKey secretKey;

        private readonly ILogger _logger;
        private readonly IOptions<MySettings> _mySetting;

        private TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        private IReadOnlyList<ICalendar> MarketCalendar { get; set; }

        private CancellationToken token;
        public string Environment { get; set; }

        public CryptoExchange SelectedCryptoExchange { get; set;} 

        #endregion

        #region constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="token"></param>
        /// <param name="key"></param>
        /// <param name="secret"></param>
        /// <param name="live"></param>
        /// <param name="mySetting"></param>
        /// <param name="logger"></param>
        public Broker(CancellationToken token, string key, string secret, string environment, IOptions<MySettings> mySetting, ILogger logger)
        {
            this.token = token;
            this._logger = logger;
            this._mySetting = mySetting;

            //alpaca client
            this.key = key;
            this.secret = secret;
            this.Environment = environment;

            this.subscribed = _mySetting.Value.Subscribed;
            
            this.SelectedCryptoExchange = (CryptoExchange)Enum.Parse(typeof(CryptoExchange), mySetting.Value.CryptoExchange);

            secretKey = new(key, secret);

            if (Environment == "Live")
            {
                alpacaTradingClient = Environments.Live.GetAlpacaTradingClient(secretKey);
                alpacaDataClient = Environments.Live.GetAlpacaDataClient(secretKey);
                alpacaCryptoDataClient = Environments.Live.GetAlpacaCryptoDataClient(secretKey);
            }
            if (Environment == "Paper")
            {
                alpacaTradingClient = Environments.Paper.GetAlpacaTradingClient(secretKey);
                alpacaDataClient = Environments.Paper.GetAlpacaDataClient(secretKey);
                alpacaCryptoDataClient = Environments.Paper.GetAlpacaCryptoDataClient(secretKey);
            }

            //streaming client
            if (subscribed)
            {
                if (Environment == "Live")
                {
                    // Connect to Alpaca's websocket and listen for updates on our orders.
                    alpacaStreamingClient = Environments.Live.GetAlpacaStreamingClient(secretKey).WithReconnect();

                    // Connect to Alpaca's websocket and listen for price updates.
                    alpacaDataStreamingClient = Environments.Live.GetAlpacaDataStreamingClient(secretKey).WithReconnect(); ;
                    alpacaCryptoStreamingClient = Environments.Live.GetAlpacaCryptoStreamingClient(secretKey).WithReconnect();
                }
                if (Environment == "Paper")
                {
                    // Connect to Alpaca's websocket and listen for updates on our orders.
                    alpacaStreamingClient = Environments.Paper.GetAlpacaStreamingClient(secretKey).WithReconnect();

                    // Connect to Alpaca's websocket and listen for price updates.
                    alpacaDataStreamingClient = Environments.Paper.GetAlpacaDataStreamingClient(secretKey).WithReconnect();
                    alpacaCryptoStreamingClient = Environments.Paper.GetAlpacaCryptoStreamingClient(secretKey).WithReconnect();
                }


                //Streaming client event
                alpacaStreamingClient.OnTradeUpdate += AlpacaStreamingClient_OnTradeUpdate;
                alpacaStreamingClient.OnError += AlpacaStreamingClient_OnError;
                alpacaStreamingClient.OnWarning += AlpacaStreamingClient_OnWarning;


                //Data Streaming client event
                alpacaDataStreamingClient.OnError += AlpacaDataStreamingClient_OnError;
                alpacaDataStreamingClient.OnWarning += AlpacaDataStreamingClient_OnWarning;
                alpacaDataStreamingClient.Connected += AlpacaDataStreamingClient_Connected;
                alpacaDataStreamingClient.SocketOpened += AlpacaDataStreamingClient_SocketOpened;
                alpacaDataStreamingClient.SocketClosed += AlpacaDataStreamingClient_SocketClosed;

                alpacaCryptoStreamingClient.OnError += AlpacaCryptoStreamingClient_OnError;
                alpacaCryptoStreamingClient.OnWarning += AlpacaCryptoStreamingClient_OnWarning;
                alpacaCryptoStreamingClient.Connected += AlpacaCryptoStreamingClient_Connected;
                alpacaCryptoStreamingClient.SocketOpened += AlpacaCryptoStreamingClient_SocketOpened;
                alpacaCryptoStreamingClient.SocketClosed += AlpacaCryptoStreamingClient_SocketClosed;

            }

            GetMarketOpenClose();

        }

        #endregion

        #region connect method
        /// <summary>
        /// Connects to the streaming API if subscribed, else runs a loop to get the data in a periodic interval
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            if (subscribed)
            {
                //connect
                await alpacaStreamingClient.ConnectAndAuthenticateAsync();
                await alpacaDataStreamingClient.ConnectAndAuthenticateAsync();
                await alpacaCryptoStreamingClient.ConnectAndAuthenticateAsync();

            }
        }
        #endregion

        #region warning and error events
        private void AlpacaStreamingClient_OnWarning(string obj)
        {
            _logger.LogWarning($"{Environment} StreamingClient Warning");
        }
        private void AlpacaStreamingClient_OnError(Exception obj)
        {
            _logger.LogError($"{Environment} StreamingClient Exception {obj.Message}");
        }

        private void AlpacaDataStreamingClient_OnWarning(string obj)
        {
            _logger.LogWarning($"{Environment} DataStreamingClient socket warning");
        }
        private void AlpacaDataStreamingClient_OnError(Exception obj)
        {
            _logger.LogError($"{Environment} DataStreamingClient socket error {obj.Message}");
        }
        private void AlpacaDataStreamingClient_SocketOpened()
        {
            _logger.LogInformation($"{Environment} DataStreamingClient socket opened");
        }
        private async void AlpacaDataStreamingClient_Connected(AuthStatus obj)
        {
            _logger.LogInformation($"{Environment} DataStreamingClient Auth status {obj.ToString()}");

            if (obj.ToString() == "Authorized")
            {
                //update for the first time after authorized
                await UpdateEnviromentData();
            }
        }
        private void AlpacaDataStreamingClient_SocketClosed()
        {
            _logger.LogInformation($"{Environment} DataStreamingClient socket closed ");
        }


        private void AlpacaCryptoStreamingClient_OnWarning(string obj)
        {
            _logger.LogWarning($"{Environment} CryptoStreamingClient Warning");
        }
        private void AlpacaCryptoStreamingClient_OnError(Exception obj)
        {
            _logger.LogError($"{Environment} CryptoStreamingClient Exception {obj.Message}");
        }
        private void AlpacaCryptoStreamingClient_SocketOpened()
        {
            _logger.LogInformation($"{Environment} CryptoStreamingClient socket opened");
        }
        private void AlpacaCryptoStreamingClient_Connected(AuthStatus obj)
        {
            _logger.LogInformation($"{Environment} CryptoStreamingClient Auth status {obj.ToString()}");

            if (obj.ToString() == "Authorized")
            {
                //update for the first time after authorized
                //await UpdateEnviromentData();
            }
        }
        private void AlpacaCryptoStreamingClient_SocketClosed()
        {
            _logger.LogInformation($"{Environment} CryptoStreamingClient socket closed ");
        }
        #endregion

        #region Market Methods
        /// <summary>
        /// Get the market open close time
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<ICalendar>> GetMarketOpenClose()
        {
            var today = DateTime.Today;
            var interval = today.AddDays(-2).GetInclusiveIntervalFromThat().WithInto(today);
            var calendar = await alpacaTradingClient.ListCalendarAsync(new CalendarRequest().SetTimeInterval(interval), token);
            var calendarDate = calendar.Last().TradingOpenTimeEst;
            var closingTime = calendar.Last().TradingCloseTimeEst;
            MarketCalendar = calendar;
            return calendar;
        }

        /// <summary>
        /// can be used to wait till the market open
        /// </summary>
        /// <returns></returns>
        private async Task AwaitMarketOpen()
        {
            while (!(await alpacaTradingClient.GetClockAsync(token)).IsOpen)
            {
                await Task.Delay(60000);
            }
        }
        #endregion

        #region Order Handling Methods

        /// <summary>
        /// Delete a open order by client id
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public async Task DeleteOpenOrder(Guid clientId)
        {
            await alpacaTradingClient.DeleteOrderAsync(clientId, token);
        }

        /// <summary>
        /// Delete all open order
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task DeleteOpenOrders(string symbol)
        {
            var orders = await alpacaTradingClient.ListOrdersAsync(new ListOrdersRequest(), token);
            foreach (var order in orders.ToList())
            {
                await alpacaTradingClient.DeleteOrderAsync(order.OrderId, token);
            }
        }

        /// <summary>
        /// Submit Order of any type
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <param name="limitPrice"></param>
        /// <param name="orderSide"></param>
        /// <param name="orderType"></param>
        /// <param name="timeInForce"></param>
        /// <returns></returns>
        public async Task<IOrder> SubmitOrder(OrderSide orderSide, OrderType orderType, TimeInForce timeInForce, bool extendedHours, string symbol, OrderQuantity quantity, Decimal? stopPrice, Decimal? limitPrice, int? trailOffsetPercentage, decimal? trailOffsetDollars)
        {
            IOrder order = null;
            try
            {
                switch (orderType)
                {
                    case OrderType.Market:
                        order = await alpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours }).ConfigureAwait(false);
                        break;
                    case OrderType.Limit:
                        order = await alpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, LimitPrice = limitPrice }).ConfigureAwait(false);
                        break;
                    case OrderType.Stop:
                        order = await alpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, StopPrice = stopPrice }).ConfigureAwait(false);
                        break;
                    case OrderType.StopLimit:
                        order = await alpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, StopPrice = stopPrice, LimitPrice = limitPrice }).ConfigureAwait(false);
                        break;
                    case OrderType.TrailingStop:
                        order = await alpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, StopPrice = stopPrice, TrailOffsetInDollars = trailOffsetDollars, TrailOffsetInPercent = trailOffsetPercentage }).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                _logger.LogInformation($"{Environment}  {ex.Message}");
            }
            return order;
        }

        /// <summary>
        /// submits a new limit order
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <param name="price"></param>
        /// <param name="orderSide"></param>
        /// <returns></returns>
        public async Task SubmitLimitOrder(string symbol, long quantity, Decimal price, OrderSide orderSide)
        {
            if (quantity == 0)
            {
                return;
            }
            try
            {
                var order = await alpacaTradingClient.PostOrderAsync(orderSide.Limit(symbol, quantity, price), token);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{Environment}  {ex.Message}");
            }
        }

        /// <summary>
        /// submits a new market order
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <param name="price"></param>
        /// <param name="orderSide"></param>
        /// <returns></returns>
        public async Task SubmitMarketOrder(string symbol, long quantity, OrderSide orderSide)
        {
            if (quantity == 0)
            {
                return;
            }
            try
            {
                var order = await alpacaTradingClient.PostOrderAsync(orderSide.Market(symbol, quantity), token);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{Environment}  {ex.Message}");
            }
        }


        /// <summary>
        /// close a position at market
        /// </summary>
        /// <param name="symbol"></param>
        private async void ClosePositionAtMarket(string symbol)
        {
            try
            {
                var positionQuantity = (await alpacaTradingClient.GetPositionAsync(symbol)).IntegerQuantity;
                Console.WriteLine("Symbol {1}, Closing position at market price.", symbol);
                if (positionQuantity > 0)
                {
                    await alpacaTradingClient.PostOrderAsync(
                        OrderSide.Sell.Market(symbol, positionQuantity), token);
                }
                else
                {
                    await alpacaTradingClient.PostOrderAsync(
                        OrderSide.Buy.Market(symbol, Math.Abs(positionQuantity)), token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{Environment} {symbol} {ex.Message}");
            }
        }

        /// <summary>
        /// Get latest trade for a symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<ITrade> GetLatestTrade(string symbol)
        {
            var asset = await GetAsset(symbol);
            try
            {
                if (asset.Class == AssetClass.Crypto)
                {
                    var ldr = new LatestDataRequest(symbol, SelectedCryptoExchange);
                    return await alpacaCryptoDataClient.GetLatestTradeAsync(ldr, token);
                }
                if (asset.Class == AssetClass.UsEquity)
                {
                    return await alpacaDataClient.GetLatestTradeAsync(symbol, token);
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get latest quote for a symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<IQuote> GetLatestQuote(string symbol)
        {
            var asset = await GetAsset(symbol);
            try
            {
                if (asset.Class == AssetClass.Crypto)
                {
                    var ldr = new LatestDataRequest(symbol, SelectedCryptoExchange);
                    return await alpacaCryptoDataClient.GetLatestQuoteAsync(ldr, token);
                }
                if (asset.Class == AssetClass.UsEquity)
                {
                    return await alpacaDataClient.GetLatestQuoteAsync(symbol, token);
                }
            }
            catch { }
            return null;
        }

        public async Task<ISnapshot> GetSnapshot(string symbol)
        {
            var asset = await GetAsset(symbol);
            if (asset.Class == AssetClass.UsEquity)
            {
                return await alpacaDataClient.GetSnapshotAsync(asset.Symbol, token);
            }
            if (asset.Class == AssetClass.Crypto)
            {
                var ieal = asset.Symbol.ToList();
                var sdr = new SnapshotDataRequest(asset.Symbol, SelectedCryptoExchange);
                return await alpacaCryptoDataClient.GetSnapshotAsync(sdr, token);
            }
            return null;
        }

        /// <summary>
        /// Get current position for a sysmbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<IPosition> GetCurrentPosition(string symbol)
        {
            try
            {
                return await alpacaTradingClient.GetPositionAsync(symbol, token);
            }
            catch { }
            return null;
        }

        #endregion

        #region Alpaca streaming Events at market level
        /// <summary>
        /// event handler to receive trade related data in your portfolio
        /// if its a new symbol then subscribe data for it
        /// </summary>
        /// <param name="obj"></param>
        private async void AlpacaStreamingClient_OnTradeUpdate(ITradeUpdate obj)
        {
            var asset = await GetAsset(obj.Order.Symbol);
            if (obj.Order.OrderStatus == OrderStatus.Filled || obj.Order.OrderStatus == OrderStatus.PartiallyFilled)
            {
                IStock stock = null;
                await Stock.Subscribe(this, obj.Order.Symbol, "Portfolio");
                stock.Qty = obj.PositionQuantity;

                var tr = obj.TimestampUtc == null ? "" : TimeZoneInfo.ConvertTimeFromUtc((DateTime)obj.TimestampUtc, easternZone).ToString();
                var tn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone).ToString();
                _logger.LogInformation($"Trade : {obj.Order.Symbol}, Current Qty: {obj.PositionQuantity}, Current Price: {obj.Price}, Trade Qty: {obj.Order.FilledQuantity}, Trade Side {obj.Order.OrderSide}, Fill Price: {obj.Order.AverageFillPrice} TradeId: {obj.Order.OrderId}, TimeEST: {tr}, Current Time: {tn}");

                await UpdateEnviromentData();
            }
            if (obj.Order.OrderStatus == OrderStatus.New || obj.Order.OrderStatus == OrderStatus.Accepted || obj.Order.OrderStatus == OrderStatus.Canceled)
            {
                await UpdateOpenOrders();
                await UpdateClosedOrders();
            }
        }

        #endregion

        #region Account Method and UI Events

        /// <summary>
        /// generate a event for UI to display account data
        /// </summary>
        /// <returns></returns>
        public async Task<IAccount> GetAccountDetails()
        {
            try
            {
                var account = await alpacaTradingClient.GetAccountAsync(token);
                return account;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{Environment} {ex.Message}");
                return null;
            }
        }

        public delegate void AccountUpdatedEventHandler(object sender, AccountUpdatedEventArgs e);

        public event EventHandler AccountUpdated;
        protected virtual void OnAccountUpdated(EventArgs e)
        {
            EventHandler handler = AccountUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Send account data as event
        /// </summary>
        /// <returns></returns>
        public async Task UpdateAccounts()
        {

            var account = await GetAccountDetails();

            AccountUpdatedEventArgs oauea = new AccountUpdatedEventArgs
            {
                Account = account
            };
            OnAccountUpdated(oauea);
        }
        #endregion

        #region Positions Method and UI Events
        /// <summary>
        /// generate a event for UI to list curent positions
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyCollection<IPosition>> ListPositions()
        {
            var positions = await alpacaTradingClient.ListPositionsAsync(token);
            return positions;
        }

        public delegate void PositionUpdatedEventHandler(object sender, PositionUpdatedEventArgs e);

        public event EventHandler PositionUpdated;
        protected virtual void OnPositionUpdated(EventArgs e)
        {
            EventHandler handler = PositionUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// send positions data as a event
        /// </summary>
        /// <returns></returns>
        public async Task UpdatePositions()
        {

            var positions = await ListPositions();

            PositionUpdatedEventArgs opuea = new PositionUpdatedEventArgs
            {
                Positions = positions
            };
            OnPositionUpdated(opuea);
        }
        #endregion

        #region Closed Orders Methods and UI Events

        /// <summary>
        /// generate a event for UI to list last 50 closed position
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyCollection<IOrder>> ClosedOrders()
        {
            ListOrdersRequest request = new()
            {
                OrderStatusFilter = OrderStatusFilter.Closed,
                LimitOrderNumber = 50
            };
            var orders = await alpacaTradingClient.ListOrdersAsync(request, token);
            return orders;
        }
        public delegate void ClosedOrderUpdatedEventHandler(object sender, ClosedOrderUpdatedEventArgs e);

        public event EventHandler ClosedOrderUpdated;

        protected virtual void OnClosedOrderUpdated(EventArgs e)
        {
            EventHandler handler = ClosedOrderUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// send closed orders as event
        /// </summary>
        /// <returns></returns>
        public async Task UpdateClosedOrders()
        {

            var closedOrders = await ClosedOrders();

            ClosedOrderUpdatedEventArgs ocouea = new ClosedOrderUpdatedEventArgs
            {
                ClosedOrders = closedOrders
            };
            OnClosedOrderUpdated(ocouea);
        }
        #endregion

        #region Open Orders Method and UI Events

        /// <summary>
        /// generate a event for UI to list open orders
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyCollection<IOrder>> OpenOrders()
        {
            ListOrdersRequest request = new()
            {
                OrderStatusFilter = OrderStatusFilter.Open
            };
            var orders = await alpacaTradingClient.ListOrdersAsync(request, token);
            return orders;
        }
        public delegate void OpenOrderUpdatedEventHandler(object sender, OpenOrderUpdatedEventArgs e);

        public event EventHandler OpenOrderUpdated;
        protected virtual void OnOpenOrderUpdated(EventArgs e)
        {
            EventHandler handler = OpenOrderUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// send open orders as a event
        /// </summary>
        /// <returns></returns>
        public async Task UpdateOpenOrders()
        {

            var openOrders = await OpenOrders();

            OpenOrderUpdatedEventArgs ooruea = new OpenOrderUpdatedEventArgs
            {
                OpenOrders = openOrders
            };
            OnOpenOrderUpdated(ooruea);
        }
        #endregion

        #region Watchlist Methods
        public async Task<IWatchList> CreateWatchList(string name, IEnumerable<string> symbols)
        {
            NewWatchListRequest newWatchListRequest = new NewWatchListRequest(name, symbols);
            return await alpacaTradingClient.CreateWatchListAsync(newWatchListRequest, token);
        }
        public async Task<IWatchList> GetWatchList(string name)
        {
            return await alpacaTradingClient.GetWatchListByNameAsync(name, token);
        }
        public async Task<IWatchList> UpdateWatchList(IWatchList wl, IEnumerable<string> symbols)
        {
            UpdateWatchListRequest updateWatchListRequest = new UpdateWatchListRequest(wl.WatchListId, wl.Name, symbols);
            return await alpacaTradingClient.UpdateWatchListByIdAsync(updateWatchListRequest, token);
        }
        public async void DeleteItemFromWatchList(IWatchList wl, string symbol)
        {
            ChangeWatchListRequest<Guid> changeWatchListRequest = new ChangeWatchListRequest<Guid>(wl.WatchListId, symbol);
            await alpacaTradingClient.DeleteAssetFromWatchListByIdAsync(changeWatchListRequest, token);
        }
        public async void AddItemToWatchList(IWatchList wl, string symbol)
        {
            ChangeWatchListRequest<Guid> changeWatchListRequest = new ChangeWatchListRequest<Guid>(wl.WatchListId, symbol);
            await alpacaTradingClient.AddAssetIntoWatchListByIdAsync(changeWatchListRequest, token);
        }
        #endregion

        #region other methods

        /// <summary>
        /// Get positions for a list of symbols
        /// </summary>
        /// <returns></returns>
        public async Task<Dictionary<string, IPosition>> GetPositionsforAssetList(IEnumerable<IAsset> assets)
        {
            Dictionary<string, IPosition> positions = new Dictionary<string, IPosition>();
            foreach(var asset in assets)
            {
                IPosition position = null;
                try
                {
                    position = await alpacaTradingClient.GetPositionAsync(asset.Symbol, token);
                }
                catch (Exception ex)
                {

                }
                positions.Add(asset.Symbol, position);
            }
            return positions;
        }

        /// <summary>
        /// Generate events to refresh UI when eniviroment changes (Live, Paper)
        /// Seperate Events for Account data, open orders, closed orders and positions
        /// Called by UI when environment changes
        /// </summary>
        /// <returns></returns>
        public async Task UpdateEnviromentData()
        {
            await UpdateAccounts();
            await UpdateOpenOrders();
            await UpdateClosedOrders();
            await UpdatePositions();
        }

        /// <summary>
        /// get a list of symbols that have a position or open order
        /// called when a position or open order is updated
        /// used by UI to update Position and Open Order watchlist 
        /// </summary>
        /// <returns></returns>
        public async Task<Dictionary<string, ISnapshot>> PositionAndOpenOrderAssets()
        {
            Dictionary<string, ISnapshot> symbolSnapShots = new();
            List<string> symbols = new();

            //all positions
            var positions = await ListPositions();
            foreach (var position in positions.ToList())
            {
                symbols.Add(position.Symbol);
            }

            //all open orders
            var openOrders = await OpenOrders();
            foreach (var order in openOrders.ToList())
            {
                symbols.Add(order.Symbol);
            }

            //find unique symbols and then get snapshots and subscribe
            var symbolList = new HashSet<string>(symbols);
            foreach (var symbol in symbolList)
            {
                var asset = await GetAsset(symbol);
                if (asset != null)
                {
                    try
                    {
                        //get snapshots
                        var ss = await GetSnapshot(symbol);
                        symbolSnapShots.Add(symbol, ss);

                        //subscribe
                        await Stock.Subscribe(this, asset.Symbol, "Portfolio");
                    }
                    catch { }
                }
            }
            return symbolSnapShots;
        }


        /// <summary>
        /// Get Asset of symbol
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<IAsset> GetAsset(string name)
        {
            return await alpacaTradingClient.GetAssetAsync(name, token);
        }

        /// <summary>
        /// get a list of asset in the market
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<IAsset>> GetAssets(AssetClass ac)
        {
            var ar = new AssetsRequest();
            ar.AssetClass = ac;
            IReadOnlyList<IAsset> assets = await alpacaTradingClient.ListAssetsAsync(ar, token);
            return assets;
        }

        /// <summary>
        /// List of Symbols with its Snapshots
        /// </summary>
        /// <param name="assets"></param>
        /// <param name="assetCount"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, ISnapshot>> ListSnapShots(IEnumerable<IAsset> assets, int assetCount)
        {
            //dictionary to hold ISnapshot for each symbol
            Dictionary<string, ISnapshot> keyValues = new();

            //List to hold ISnapshot
            List<ISnapshot> snapshots = new();

            //get ISnapshot of stock symbols for assetCount at a time
            for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += assetCount)
            {
                var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(assetCount);
                var stockSnapshots = await alpacaDataClient.ListSnapshotsAsync(assetSubset.Select(x => x.Symbol), token);
                foreach (var item in stockSnapshots)
                {
                    keyValues.Add(item.Key, item.Value);
                }
            }
            //get ISnapshot of crypto symbols for assetCount at a time
            for (int i = 0; i < assets.Where(x => x.Class == AssetClass.Crypto).Count(); i += assetCount)
            {
                var assetSubset = assets.Where(x => x.Class == AssetClass.Crypto).Skip(i).Take(assetCount);
                var sdlr = new SnapshotDataListRequest(assetSubset.Select(x => x.Symbol), SelectedCryptoExchange);
                var cryptoSnapshots = await alpacaCryptoDataClient.ListSnapshotsAsync(sdlr, token);
                foreach (var item in cryptoSnapshots)
                {
                    keyValues.Add(item.Key, item.Value);
                }
            }
            return keyValues;
        }

        /// <summary>
        /// Gets Latest Trades for a symbol list
        /// </summary>
        /// <param name="assets"></param>
        /// <param name="assetCount"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, ITrade>> ListTrades(IEnumerable<IAsset> assets, int assetCount)
        {
            //dictionary to hold ISnapshot for each symbol
            Dictionary<string, ITrade> keyValues = new();

            //List to hold ISnapshot
            List<ITrade> trades = new();

            //get ISnapshot of stock symbols for assetCount at a time
            for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += assetCount)
            {
                var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(assetCount);
                var stockTrades = await alpacaDataClient.ListLatestTradesAsync(assetSubset.Select(x => x.Symbol), token);
                foreach (var item in stockTrades)
                {
                    keyValues.Add(item.Key, item.Value);
                }
            }
            //get ISnapshot of crypto symbols for assetCount at a time
            for (int i = 0; i < assets.Where(x => x.Class == AssetClass.Crypto).Count(); i += assetCount)
            {
                var assetSubset = assets.Where(x => x.Class == AssetClass.Crypto).Skip(i).Take(assetCount);
                var ldlr = new LatestDataListRequest(assetSubset.Select(x => x.Symbol), SelectedCryptoExchange);
                var cryptoSnapshots = await alpacaCryptoDataClient.ListLatestTradesAsync(ldlr, token);
                foreach (var item in cryptoSnapshots)
                {
                    keyValues.Add(item.Key, item.Value);
                }
            }
            return keyValues;
        }

        /// <summary>
        /// List of symbols and its Bars
        /// </summary>
        /// <param name="assets"></param>
        /// <param name="barTimeFrameUnit"></param>
        /// <param name="barTimeFrameCount"></param>
        /// <param name="assetCount"></param>
        /// <param name="toDate"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, List<IBar>>> ListHistoricalBars(IEnumerable<IAsset> assets, BarTimeFrameUnit barTimeFrameUnit, int barTimeFrameCount, int assetCount, DateTime toDate)
        {
            
            //get the FromDate based on barTimeFrameUnit, barTimeFrameCount and toDate  (barTimeFrame can be 20Day, 15Min, 5Weeks etc)
            var fromDate = await GetTimeIntervalFrom(new BarTimeFrame(barTimeFrameCount, barTimeFrameUnit), toDate);

            //define barTimeFrame of one unit required by the api
            var barTimeFrame = new BarTimeFrame(1, barTimeFrameUnit);

            //dictionary to hold Ibars for each symbol
            Dictionary<string, List<IBar>> symbolAndBars = new();

            //List to hold IBar
            List<IBar> bars = new();

            //get a historical Ibars of stock symbols for assetCount at a time
            for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += assetCount)
            {
                var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(assetCount);
                var historicalBarsRequest = new HistoricalBarsRequest(assetSubset.Select(x => x.Symbol), fromDate, toDate, barTimeFrame);
                await foreach (var bar in alpacaDataClient.GetHistoricalBarsAsAsyncEnumerable(historicalBarsRequest, token))
                {
                    bars.Add(bar);
                }
            }
            //get a historical Ibars of crypto symbols for assetCount at a time
            for (int i = 0; i < assets.Where(x => x.Class == AssetClass.Crypto).Count(); i += assetCount)
            {
                var assetSubset = assets.Where(x => x.Class == AssetClass.Crypto).Skip(i).Take(assetCount);
                var historicalBarsRequest = new HistoricalCryptoBarsRequest(assetSubset.Select(x => x.Symbol), fromDate, toDate, barTimeFrame);
                await foreach (var bar in alpacaCryptoDataClient.GetHistoricalBarsAsAsyncEnumerable(historicalBarsRequest, token))
                {
                    bars.Add(bar);
                }
            }
            symbolAndBars = bars.GroupBy(x => x.Symbol).ToDictionary(g => g.Key, g => g.ToList());
            return symbolAndBars;
        }

        /// <summary>
        /// Calculates the from DateTime based on current Date and BartTimeFrame
        /// </summary>
        /// <param name="barTimeFrame"></param>
        /// <param name="toDate"></param>
        /// <returns></returns>
        public async Task<DateTime> GetTimeIntervalFrom(BarTimeFrame barTimeFrame, DateTime toDate)
        {
            DateTime fromDate = toDate;
            switch (barTimeFrame.Unit)
            {
                case BarTimeFrameUnit.Minute:
                    fromDate = toDate.AddMinutes(-barTimeFrame.Value);
                    break;
                case BarTimeFrameUnit.Hour:
                    fromDate = toDate.AddHours(-barTimeFrame.Value);
                    break;
                case BarTimeFrameUnit.Day:
                    fromDate = toDate.AddDays(-barTimeFrame.Value);
                    break;
                case BarTimeFrameUnit.Week:
                    fromDate = toDate.AddDays(-barTimeFrame.Value * 7);
                    break;
                case BarTimeFrameUnit.Month:
                    fromDate = toDate.AddMonths(-barTimeFrame.Value);
                    break;
            }
            return fromDate;
        }



        /// <summary>
        /// dispose
        /// </summary>
        public void Dispose()
        {
            alpacaTradingClient?.Dispose();
            alpacaDataClient?.Dispose();
            alpacaCryptoDataClient?.Dispose();

            alpacaStreamingClient?.Dispose();
            alpacaDataStreamingClient?.Dispose();
            alpacaCryptoStreamingClient?.Dispose();
        }

        #endregion
    }

    #region Event Arg classes
    public class AccountUpdatedEventArgs : EventArgs
    {
        public IAccount? Account { get; set; }
    }

    public class PositionUpdatedEventArgs : EventArgs
    {
        public IReadOnlyCollection<IPosition>? Positions { get; set; }
    }
    public class ClosedOrderUpdatedEventArgs : EventArgs
    {
        public IReadOnlyCollection<IOrder>? ClosedOrders { get; set; }
    }
    public class OpenOrderUpdatedEventArgs : EventArgs
    {
        public IReadOnlyCollection<IOrder>? OpenOrders { get; set; }
    }

    #endregion

}
