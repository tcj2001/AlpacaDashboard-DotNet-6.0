global using Alpaca.Markets;
global using Alpaca.Markets.Extensions;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using AlpacaEnvironment = Alpaca.Markets.Environments;
global using AlpacaDashboard.Enums;
global using static AlpacaDashboard.Helpers.DateHelper;

namespace AlpacaDashboard.Brokers;

/// <summary>
/// This class handles all request related Alpaca Market and Data api
/// </summary>
public class Broker : IDisposable
{
    #region public and private properties
    private string key;
    private string secret;
    public bool subscribed;

    public IAlpacaTradingClient AlpacaTradingClient { get; set; } = default!;

    public IAlpacaDataClient AlpacaDataClient { get; set; } = default!;
    public IAlpacaDataStreamingClient AlpacaDataStreamingClient { get; set; } = default!;
    public IAlpacaStreamingClient AlpacaStreamingClient { get; set; } = default!;

    private SecretKey secretKey;

    private readonly ILogger _logger;
    private readonly IOptions<MySettings> _mySetting;

    private TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    private IReadOnlyList<ICalendar> MarketCalendar { get; set; } = default!;

    private CancellationToken token;
    public TradingEnvironment Environment { get; set; }

    public CryptoExchange SelectedCryptoExchange { get; set; }

    static public IAlpacaCryptoDataClient AlpacaCryptoDataClient { get; set; } = default!;
    static public IAlpacaCryptoStreamingClient AlpacaCryptoStreamingClient { get; set; } = default!;
    static bool CryptoConnected = false;
    static TradingEnvironment CryptoConnectedEnvironment { get; set; }

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
    public Broker(string key, string secret, TradingEnvironment environment, IOptions<MySettings> mySetting, ILogger logger, CancellationToken token)
    {
        this.token = token;
        _logger = logger;
        _mySetting = mySetting;

        //alpaca client
        this.key = key;
        this.secret = secret;
        Environment = environment;

        subscribed = _mySetting.Value.Subscribed;

        SelectedCryptoExchange = (CryptoExchange)Enum.Parse(typeof(CryptoExchange), mySetting.Value.CryptoExchange);

        secretKey = new(key, secret);

        if (Environment == TradingEnvironment.Live)
        {
            AlpacaTradingClient = AlpacaEnvironment.Live.GetAlpacaTradingClient(secretKey);
            AlpacaDataClient = AlpacaEnvironment.Live.GetAlpacaDataClient(secretKey);

            //connect only in one environment
            if (!CryptoConnected)
            {
                AlpacaCryptoDataClient = AlpacaEnvironment.Live.GetAlpacaCryptoDataClient(secretKey);
                CryptoConnectedEnvironment = Environment;
                CryptoConnected = true;
            }
        }
        if (Environment == TradingEnvironment.Paper)
        {
            AlpacaTradingClient = AlpacaEnvironment.Paper.GetAlpacaTradingClient(secretKey);
            AlpacaDataClient = AlpacaEnvironment.Paper.GetAlpacaDataClient(secretKey);

            //connect only in one environment
            if (!CryptoConnected)
            {
                AlpacaCryptoDataClient = AlpacaEnvironment.Paper.GetAlpacaCryptoDataClient(secretKey);
                CryptoConnectedEnvironment = Environment;
                CryptoConnected = true;
            }
        }

        //streaming client
        if (subscribed)
        {
            if (Environment == TradingEnvironment.Live)
            {
                // Connect to Alpaca's websocket and listen for updates on our orders.
                AlpacaStreamingClient = AlpacaEnvironment.Live.GetAlpacaStreamingClient(secretKey).WithReconnect();

                // Connect to Alpaca's websocket and listen for price updates.
                AlpacaDataStreamingClient = AlpacaEnvironment.Live.GetAlpacaDataStreamingClient(secretKey).WithReconnect();

                //connect only in one environment
                if (CryptoConnectedEnvironment == Environment)
                    AlpacaCryptoStreamingClient = AlpacaEnvironment.Live.GetAlpacaCryptoStreamingClient(secretKey).WithReconnect();
            }
            if (Environment == TradingEnvironment.Paper)
            {
                // Connect to Alpaca's websocket and listen for updates on our orders.
                AlpacaStreamingClient = AlpacaEnvironment.Paper.GetAlpacaStreamingClient(secretKey).WithReconnect();

                // Connect to Alpaca's websocket and listen for price updates.
                AlpacaDataStreamingClient = AlpacaEnvironment.Paper.GetAlpacaDataStreamingClient(secretKey).WithReconnect();

                //connect only in one environment
                if (CryptoConnectedEnvironment == Environment)
                    AlpacaCryptoStreamingClient = AlpacaEnvironment.Paper.GetAlpacaCryptoStreamingClient(secretKey).WithReconnect();
            }

            //Streaming client event
            AlpacaStreamingClient.OnTradeUpdate += AlpacaStreamingClient_OnTradeUpdate;
            AlpacaStreamingClient.OnError += AlpacaStreamingClient_OnError;
            AlpacaStreamingClient.OnWarning += AlpacaStreamingClient_OnWarning;

            //Data Streaming client event
            AlpacaDataStreamingClient.OnError += AlpacaDataStreamingClient_OnError;
            AlpacaDataStreamingClient.OnWarning += AlpacaDataStreamingClient_OnWarning;
            AlpacaDataStreamingClient.Connected += AlpacaDataStreamingClient_Connected;
            AlpacaDataStreamingClient.SocketOpened += AlpacaDataStreamingClient_SocketOpened;
            AlpacaDataStreamingClient.SocketClosed += AlpacaDataStreamingClient_SocketClosed;

            AlpacaCryptoStreamingClient.OnError += AlpacaCryptoStreamingClient_OnError;
            AlpacaCryptoStreamingClient.OnWarning += AlpacaCryptoStreamingClient_OnWarning;
            AlpacaCryptoStreamingClient.Connected += AlpacaCryptoStreamingClient_Connected;
            AlpacaCryptoStreamingClient.SocketOpened += AlpacaCryptoStreamingClient_SocketOpened;
            AlpacaCryptoStreamingClient.SocketClosed += AlpacaCryptoStreamingClient_SocketClosed;
        }
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
            await AlpacaStreamingClient.ConnectAndAuthenticateAsync().ConfigureAwait(false);
            await AlpacaDataStreamingClient.ConnectAndAuthenticateAsync().ConfigureAwait(false);

            //connect only in one environment
            if (CryptoConnectedEnvironment == Environment)
            {
                await AlpacaCryptoStreamingClient.ConnectAndAuthenticateAsync().ConfigureAwait(false);
            }
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
            await UpdateEnviromentData().ConfigureAwait(false);
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

    #region Order Handling Methods

    /// <summary>
    /// Delete a open order by order id
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<bool> DeleteOpenOrder(Guid orderId)
    {
        return await AlpacaTradingClient.DeleteOrderAsync(orderId, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete all open order
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task DeleteOpenOrders(string symbol)
    {
        var orders = await AlpacaTradingClient.ListOrdersAsync(new ListOrdersRequest(), token).ConfigureAwait(false);

        foreach (var order in orders)
        {
            await AlpacaTradingClient.DeleteOrderAsync(order.OrderId, token).ConfigureAwait(false);
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
    public async Task<(IOrder?, string?)> SubmitOrder(OrderSide orderSide, OrderType orderType, TimeInForce timeInForce, bool extendedHours, string symbol, OrderQuantity quantity, decimal? stopPrice,
        decimal? limitPrice, int? trailOffsetPercentage, decimal? trailOffsetDollars)
    {
        IOrder? order = null;
        string? message = null;

        try
        {
            switch (orderType)
            {
                case OrderType.Market:
                    order = await AlpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours }).ConfigureAwait(false);
                    break;
                case OrderType.Limit:
                    order = await AlpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, 
                        LimitPrice = limitPrice }).ConfigureAwait(false);
                    break;
                case OrderType.Stop:
                    order = await AlpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, 
                        StopPrice = stopPrice }).ConfigureAwait(false);
                    break;
                case OrderType.StopLimit:
                    order = await AlpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, 
                        StopPrice = stopPrice, LimitPrice = limitPrice }).ConfigureAwait(false);
                    break;
                case OrderType.TrailingStop:
                    order = await AlpacaTradingClient.PostOrderAsync(new NewOrderRequest(symbol, quantity, orderSide, orderType, timeInForce) { ExtendedHours = extendedHours, 
                        StopPrice = stopPrice, TrailOffsetInDollars = trailOffsetDollars, TrailOffsetInPercent = trailOffsetPercentage }).ConfigureAwait(false);
                    break;
            }
            return (order, message);
        }
        catch (Exception ex)
        {
            //MessageBox.Show(ex.Message);
            _logger.LogInformation($"{Environment}  {message + ":" + ex.Message}");
            return (null, message + ":" + ex.Message);
        }
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
            var order = await AlpacaTradingClient.PostOrderAsync(orderSide.Limit(symbol, quantity, price), token).ConfigureAwait(false);
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
            var order = await AlpacaTradingClient.PostOrderAsync(orderSide.Market(symbol, quantity), token).ConfigureAwait(false);
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
            var positionQuantity = (await AlpacaTradingClient.GetPositionAsync(symbol).ConfigureAwait(false)).IntegerQuantity;

            Console.WriteLine("Symbol {1}, Closing position at market price.", symbol);
            if (positionQuantity > 0)
            {
                await AlpacaTradingClient.PostOrderAsync(OrderSide.Sell.Market(symbol, positionQuantity), token).ConfigureAwait(false);
            }
            else
            {
                await AlpacaTradingClient.PostOrderAsync(OrderSide.Buy.Market(symbol, Math.Abs(positionQuantity)), token).ConfigureAwait(false);
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
    public async Task<ITrade?> GetLatestTrade(string symbol)
    {
        var asset = await GetAsset(symbol).ConfigureAwait(false);

        try
        {
            if (asset.Class == AssetClass.Crypto)
            {
                var ldr = new LatestDataRequest(symbol, SelectedCryptoExchange);

                return await AlpacaCryptoDataClient.GetLatestTradeAsync(ldr, token).ConfigureAwait(false);
            }
            if (asset.Class == AssetClass.UsEquity)
            {
                return await AlpacaDataClient.GetLatestTradeAsync(symbol, token).ConfigureAwait(false);
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
    public async Task<IQuote?> GetLatestQuote(string symbol)
    {
        var asset = await GetAsset(symbol).ConfigureAwait(false);

        try
        {
            if (asset.Class == AssetClass.Crypto)
            {
                var ldr = new LatestDataRequest(symbol, SelectedCryptoExchange);

                return await AlpacaCryptoDataClient.GetLatestQuoteAsync(ldr, token).ConfigureAwait(false);
            }
            if (asset.Class == AssetClass.UsEquity)
            {
                return await AlpacaDataClient.GetLatestQuoteAsync(symbol, token).ConfigureAwait(false);
            }
        }
        catch { }

        return null;
    }

    public async Task<ISnapshot?> GetSnapshot(string symbol)
    {
        var asset = await GetAsset(symbol).ConfigureAwait(false);

        if (asset.Class == AssetClass.UsEquity)
        {
            return await AlpacaDataClient.GetSnapshotAsync(asset.Symbol, token).ConfigureAwait(false);
        }
        if (asset.Class == AssetClass.Crypto)
        {
            //var ieal = asset.Symbol.ToList();
            var sdr = new SnapshotDataRequest(asset.Symbol, SelectedCryptoExchange);

            return await AlpacaCryptoDataClient.GetSnapshotAsync(sdr, token).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Get current position for a sysmbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<IPosition?> GetCurrentPosition(string symbol)
    {
        try
        {
            return await AlpacaTradingClient.GetPositionAsync(symbol, token).ConfigureAwait(false);
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
        var asset = await GetAsset(obj.Order.Symbol).ConfigureAwait(false);

        if (obj.Order.OrderStatus == OrderStatus.Filled || obj.Order.OrderStatus == OrderStatus.PartiallyFilled)
        {
            IStock? stock = null;
            await Stock.Subscribe(this, obj.Order.Symbol, "Portfolio").ConfigureAwait(false);

            if (stock != null)
            {
                stock.TradeUpdate = obj;
            }

            var tr = obj.TimestampUtc == null ? "" : TimeZoneInfo.ConvertTimeFromUtc((DateTime)obj.TimestampUtc, easternZone).ToString();
            var tn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone).ToString();
            _logger.LogInformation($"Trade : {obj.Order.Symbol}, Current Qty: {obj.PositionQuantity}, Current Price: {obj.Price}, Trade Qty: {obj.Order.FilledQuantity}, " +
                $"Trade Side {obj.Order.OrderSide}, Fill Price: {obj.Order.AverageFillPrice} TradeId: {obj.Order.OrderId}, TimeEST: {tr}, Current Time: {tn}");

            await UpdateEnviromentData().ConfigureAwait(false);
        }
        if (obj.Order.OrderStatus == OrderStatus.New || obj.Order.OrderStatus == OrderStatus.Accepted || obj.Order.OrderStatus == OrderStatus.Canceled)
        {
            await UpdateOpenOrders().ConfigureAwait(false);
            await UpdateClosedOrders().ConfigureAwait(false);
        }
    }

    #endregion

    #region Account Method and UI Events

    /// <summary>
    /// Get Account data
    /// </summary>
    /// <returns></returns>
    public async Task<IAccount?> GetAccountDetails()
    {
        try
        {
            return await AlpacaTradingClient.GetAccountAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Environment} {ex.Message}");
        }

        return null;
    }

    public delegate void AccountUpdatedEventHandler(object sender, AccountUpdatedEventArgs e);

    public event EventHandler AccountUpdated = default!;
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
        var account = await GetAccountDetails().ConfigureAwait(false);

        AccountUpdatedEventArgs oauea = new()
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
        return await AlpacaTradingClient.ListPositionsAsync(token).ConfigureAwait(false);
    }

    public delegate void PositionUpdatedEventHandler(object sender, PositionUpdatedEventArgs e);

    public event EventHandler PositionUpdated = default!;
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

        var positions = await ListPositions().ConfigureAwait(false);

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

        return await AlpacaTradingClient.ListOrdersAsync(request, token).ConfigureAwait(false);
    }

    public delegate void ClosedOrderUpdatedEventHandler(object sender, ClosedOrderUpdatedEventArgs e);

    public event EventHandler ClosedOrderUpdated = default!;

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
        var closedOrders = await ClosedOrders().ConfigureAwait(false);

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

        return await AlpacaTradingClient.ListOrdersAsync(request, token).ConfigureAwait(false);
    }

    public delegate void OpenOrderUpdatedEventHandler(object sender, OpenOrderUpdatedEventArgs e);

    public event EventHandler OpenOrderUpdated = default!;
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
        var openOrders = await OpenOrders().ConfigureAwait(false);

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
        return await AlpacaTradingClient.CreateWatchListAsync(new NewWatchListRequest(name, symbols), token).ConfigureAwait(false);
    }

    public async Task<IWatchList> GetWatchList(string name)
    {
        return await AlpacaTradingClient.GetWatchListByNameAsync(name, token).ConfigureAwait(false);
    }

    public async Task<IWatchList> UpdateWatchList(IWatchList wl, IEnumerable<IAsset> assets)
    {
        var symbols = assets.Select(x => x.Symbol).ToList();
        UpdateWatchListRequest updateWatchListRequest = new UpdateWatchListRequest(wl.WatchListId, wl.Name, symbols);
        return await AlpacaTradingClient.UpdateWatchListByIdAsync(updateWatchListRequest, token).ConfigureAwait(false);
    }

    public async void DeleteItemFromWatchList(IWatchList wl, IAsset asset)
    {
        ChangeWatchListRequest<Guid> changeWatchListRequest = new ChangeWatchListRequest<Guid>(wl.WatchListId, asset.Symbol);
        await AlpacaTradingClient.DeleteAssetFromWatchListByIdAsync(changeWatchListRequest, token).ConfigureAwait(false);
    }

    public async void AddItemToWatchList(IWatchList wl, string symbol)
    {
        await AlpacaTradingClient.AddAssetIntoWatchListByIdAsync(new ChangeWatchListRequest<Guid>(wl.WatchListId, symbol), token).ConfigureAwait(false);
    }
    #endregion

    #region other methods

    /// <summary>
    /// Get positions for a list of symbols
    /// </summary>
    /// <returns></returns>
    public async Task<Dictionary<IAsset, IPosition?>> GetPositionsforAssetList(IEnumerable<IAsset> assets)
    {
        Dictionary<IAsset, IPosition?> positions = new();

        foreach (var asset in assets)
        {
            IPosition? position = null;
            try
            {
                position = await AlpacaTradingClient.GetPositionAsync(asset.Symbol, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            positions.Add(asset, position);
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
        await UpdateAccounts().ConfigureAwait(false);
        await UpdateOpenOrders().ConfigureAwait(false);
        await UpdateClosedOrders().ConfigureAwait(false);
        await UpdatePositions().ConfigureAwait(false);
    }

    /// <summary>
    /// get a list of symbols that have a position or open order
    /// called when a position or open order is updated
    /// used by UI to update Position and Open Order watchlist 
    /// </summary>
    /// <returns></returns>
    public async Task<Dictionary<IAsset, ISnapshot?>> PositionAndOpenOrderAssets()
    {
        Dictionary<IAsset, ISnapshot?> assetAndSnapShots = new();
        List<string> symbols = new();

        //all positions
        var positions = await ListPositions().ConfigureAwait(false);
        foreach (var position in positions.ToList())
        {
            symbols.Add(position.Symbol);
        }

        //all open orders
        var openOrders = await OpenOrders().ConfigureAwait(false);
        foreach (var order in openOrders.ToList())
        {
            symbols.Add(order.Symbol);
        }

        //find unique symbols and then get snapshots and subscribe
        var symbolList = new HashSet<string>(symbols);
        foreach (var symbol in symbolList)
        {
            var asset = await GetAsset(symbol).ConfigureAwait(false);

            if (asset != null)
            {
                try
                {
                    //get snapshots
                    var ss = await GetSnapshot(symbol).ConfigureAwait(false);

                    if (ss != null)
                    {
                        assetAndSnapShots.Add(asset, ss);
                    }

                    //subscribe
                    await Stock.Subscribe(this, asset.Symbol, "Portfolio").ConfigureAwait(false);
                }
                catch { }
            }
        }

        return assetAndSnapShots;
    }


    /// <summary>
    /// Get Asset of symbol
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public async Task<IAsset> GetAsset(string name)
    {
        return await AlpacaTradingClient.GetAssetAsync(name, token).ConfigureAwait(false);
    }

    /// <summary>
    /// get a list of asset in the market
    /// </summary>
    /// <returns></returns>
    public async Task<IReadOnlyList<IAsset>> GetAssets(AssetClass ac)
    {
        var ar = new AssetsRequest();
        ar.AssetClass = ac;

        return await AlpacaTradingClient.ListAssetsAsync(ar, token).ConfigureAwait(false);
    }

    /// <summary>
    /// List of Symbols with its Snapshots
    /// </summary>
    /// <param name="assets"></param>
    /// <param name="maxAssetsAtOneTime"></param>
    /// <returns></returns>
    public async Task<Dictionary<IAsset, ISnapshot?>> ListSnapShots(IEnumerable<IAsset?> assets, int maxAssetsAtOneTime)
    {
        //dictionary to hold ISnapshot for each symbol
        Dictionary<IAsset, ISnapshot?> keyValues = new();

        //List to hold ISnapshot
        List<ISnapshot> snapshots = new();

        //get ISnapshot of stock symbols for assetCount at a time
        for (int i = 0; i < assets.Where(x => x?.Class == AssetClass.UsEquity).Count(); i += maxAssetsAtOneTime)
        {
            var assetSubset = assets.Where(x => x != null && x.Class == AssetClass.UsEquity).Skip(i).Take(maxAssetsAtOneTime).Select(x => x != null ? x.Symbol : string.Empty);
            var stockSnapshots = await AlpacaDataClient.ListSnapshotsAsync(assetSubset, token).ConfigureAwait(false);

            foreach (var item in stockSnapshots)
            {
                var asset = assets.Where(x => x?.Symbol == item.Key).First();
                if (asset != null)
                    keyValues.Add(asset, item.Value);
            }
        }

        //get ISnapshot of crypto symbols for assetCount at a time
        for (int i = 0; i < assets.Where(x => x?.Class == AssetClass.Crypto).Count(); i += maxAssetsAtOneTime)
        {
            var assetSubset = assets.Where(x => x?.Class == AssetClass.Crypto).Skip(i).Take(maxAssetsAtOneTime).Select(x => x != null ? x.Symbol : string.Empty);
            var sdlr = new SnapshotDataListRequest(assetSubset, SelectedCryptoExchange);
            var cryptoSnapshots = await AlpacaCryptoDataClient.ListSnapshotsAsync(sdlr, token).ConfigureAwait(false);

            foreach (var item in cryptoSnapshots)
            {
                var asset = assets.Where(x => x?.Symbol == item.Key).First();
                if (asset != null)
                    keyValues.Add(asset, item.Value);
            }
        }

        return keyValues;
    }

    /// <summary>
    /// Gets Latest Trades for a symbol list
    /// </summary>
    /// <param name="assets"></param>
    /// <param name="maxAssetsAtOneTime"></param>
    /// <returns></returns>
    public async Task<Dictionary<string, ITrade>> ListTrades(IEnumerable<IAsset?> assets, int maxAssetsAtOneTime)
    {
        //dictionary to hold ISnapshot for each symbol
        Dictionary<string, ITrade> keyValues = new();

        //List to hold ISnapshot
        List<ITrade> trades = new();

        //get ISnapshot of stock symbols for assetCount at a time
        for (int i = 0; i < assets.Where(x => x?.Class == AssetClass.UsEquity).Count(); i += maxAssetsAtOneTime)
        {
            var assetSubset = assets.Where(x => x?.Class == AssetClass.UsEquity).Skip(i).Take(maxAssetsAtOneTime).Select(x => x != null ? x.Symbol : string.Empty);
            var stockTrades = await AlpacaDataClient.ListLatestTradesAsync(assetSubset, token).ConfigureAwait(false);

            foreach (var item in stockTrades)
            {
                keyValues.Add(item.Key, item.Value);
            }
        }

        //get ISnapshot of crypto symbols for assetCount at a time
        for (int i = 0; i < assets.Where(x => x?.Class == AssetClass.Crypto).Count(); i += maxAssetsAtOneTime)
        {
            var assetSubset = assets.Where(x => x?.Class == AssetClass.Crypto).Skip(i).Take(maxAssetsAtOneTime).Select(x => x != null ? x.Symbol : string.Empty);
            var ldlr = new LatestDataListRequest(assetSubset, SelectedCryptoExchange);
            var cryptoSnapshots = await AlpacaCryptoDataClient.ListLatestTradesAsync(ldlr, token).ConfigureAwait(false);

            foreach (var item in cryptoSnapshots)
            {
                keyValues.Add(item.Key, item.Value);
            }
        }

        return keyValues;
    }

    /// <summary>
    /// Get Historical bars for a asset
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="barTimeFrameUnit"></param>
    /// <param name="barTimeFrameCount"></param>
    /// <param name="toDate"></param>
    /// <returns></returns>
    public async Task<IEnumerable<IBar>> GetHistoricalBar(IAsset? asset, BarTimeFrame barTimeFrame, int noOfBars, DateTime toDate)
    {
        //get the FromDate based on barTimeFrameUnit, barTimeFrameCount and toDate  (barTimeFrame can be 20Day, 15Min, 5Weeks etc)
        var fromDate = GetTimeIntervalFrom(barTimeFrame, noOfBars, toDate);

        List<IBar> bars = new();
        if (asset?.Class == AssetClass.UsEquity)
        {
            var historicalBarsRequest = new HistoricalBarsRequest(asset.Symbol, fromDate, toDate, barTimeFrame);
            await foreach (var bar in AlpacaDataClient.GetHistoricalBarsAsAsyncEnumerable(historicalBarsRequest, token))
            {
                bars.Add(bar);
            }
        }
        if (asset?.Class == AssetClass.Crypto)
        {
            var historicalBarsRequest = new HistoricalCryptoBarsRequest(asset.Symbol, fromDate, toDate, barTimeFrame);
            await foreach (var bar in AlpacaCryptoDataClient.GetHistoricalBarsAsAsyncEnumerable(historicalBarsRequest, token))
            {
                bars.Add(bar);
            }
        }
        return bars;
    }

    /// <summary>
    /// List of symbols and its Bars
    /// </summary>
    /// <param name="assets"></param>
    /// <param name="barTimeFrameUnit"></param>
    /// <param name="barTimeFrameCount"></param>
    /// <param name="maxAssetsAtOneTime"></param>
    /// <param name="toDate"></param>
    /// <returns></returns>
    public async Task<Dictionary<IAsset, List<IBar>>> ListHistoricalBars(IEnumerable<IAsset> assets, BarTimeFrame barTimeFrame, int noOfBars, int maxAssetsAtOneTime, DateTime toDate)
    {
        //get the FromDate based on barTimeFrameUnit, barTimeFrameCount and toDate  (barTimeFrame can be 20Day, 15Min, 5Weeks etc)
        var fromDate = GetTimeIntervalFrom(barTimeFrame, noOfBars, toDate);

        //dictionary to hold Ibars for each symbol
        Dictionary<IAsset, List<IBar>> assetAndBars = new();

        //List to hold IBar
        List<IBar> bars = new();

        //get a historical Ibars of stock symbols for assetCount at a time
        for (int i = 0; i < assets.Where(x => x.Class == AssetClass.UsEquity).Count(); i += maxAssetsAtOneTime)
        {
            var assetSubset = assets.Where(x => x.Class == AssetClass.UsEquity).Skip(i).Take(maxAssetsAtOneTime);
            var historicalBarsRequest = new HistoricalBarsRequest(assetSubset.Select(x => x.Symbol), fromDate, toDate, barTimeFrame);

            await foreach (var bar in AlpacaDataClient.GetHistoricalBarsAsAsyncEnumerable(historicalBarsRequest, token))
            {
                bars.Add(bar);
            }
        }

        //get a historical Ibars of crypto symbols for assetCount at a time
        for (int i = 0; i < assets.Where(x => x.Class == AssetClass.Crypto).Count(); i += maxAssetsAtOneTime)
        {
            var assetSubset = assets.Where(x => x.Class == AssetClass.Crypto).Skip(i).Take(maxAssetsAtOneTime);
            var historicalBarsRequest = new HistoricalCryptoBarsRequest(assetSubset.Select(x => x.Symbol), fromDate, toDate, barTimeFrame);

            await foreach (var bar in AlpacaCryptoDataClient.GetHistoricalBarsAsAsyncEnumerable(historicalBarsRequest, token))
            {
                bars.Add(bar);
            }
        }

        return bars.GroupBy(x => x.Symbol).ToDictionary(g => assets.Where(a => a.Symbol == g.Key).Select(a => a).First(), g => g.ToList());
    }

    /// <summary>
    /// Calculates the from DateTime based on current Date and BartTimeFrame
    /// </summary>
    /// <param name="barTimeFrame"></param>
    /// <param name="toDate"></param>
    /// <returns></returns>
    public static DateTime GetTimeIntervalFrom(BarTimeFrame barTimeFrame, int noOfBars, DateTime toDate)
    {
        DateTime fromDate = toDate;
        switch (barTimeFrame.Unit)
        {
            case BarTimeFrameUnit.Minute:
                fromDate = toDate.AddMinutes(-barTimeFrame.Value * noOfBars);
                break;
            case BarTimeFrameUnit.Hour:
                fromDate = toDate.AddHours(-barTimeFrame.Value * noOfBars);
                break;
            case BarTimeFrameUnit.Day:
                fromDate = toDate.AddDays(-barTimeFrame.Value * noOfBars);
                break;
            case BarTimeFrameUnit.Week:
                fromDate = toDate.AddDays(-barTimeFrame.Value * 7 * noOfBars);
                break;
            case BarTimeFrameUnit.Month:
                fromDate = toDate.AddMonths(-barTimeFrame.Value * noOfBars);
                break;
        }

        return fromDate;
    }



    /// <summary>
    /// dispose
    /// </summary>
    public void Dispose()
    {
        AlpacaTradingClient?.Dispose();
        AlpacaDataClient?.Dispose();
        AlpacaCryptoDataClient?.Dispose();

        AlpacaStreamingClient?.Dispose();
        AlpacaDataStreamingClient?.Dispose();
        AlpacaCryptoStreamingClient?.Dispose();
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

