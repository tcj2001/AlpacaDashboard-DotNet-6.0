global using System.Reflection;

namespace AlpacaDashboard;

/// <summary>
/// UI to display all market related info
/// List scanner
/// List Bots
/// </summary>
public partial class AlpacaDashboard : Form
{
    #region public and private properties
    private readonly ILogger _logger;
    private readonly IOptions<MySettings> _mySettings;
    private readonly IOptions<LiveKey> _liveKey;
    private readonly IOptions<PaperKey> _paperKey;
    private Broker PaperBroker { get; set; } = default!;
    private Broker LiveBroker { get; set; } = default!;

    private TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private CancellationToken token;
    private string Environment { get; set; }
    #endregion

    #region constructor
    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="mySettings"></param>
    /// <param name="liveKey"></param>
    /// <param name="paperKey"></param>
    public AlpacaDashboard(ILogger<AlpacaDashboard> logger, IOptions<MySettings> mySettings, IOptions<LiveKey> liveKey, IOptions<PaperKey> paperKey)
    {
        _logger = logger;
        _mySettings = mySettings;
        _liveKey = liveKey;
        _paperKey = paperKey;
        token = new CancellationToken();

        InitializeComponent();

        this.Environment = "Paper";

        if (this.Environment == "Live")
        {
            checkBoxLivePaper.Checked = true;
        }

        //order box default
        comboBoxMarketOrLimit.DataSource = new List<string>() { "Market", "Limit", "Stop", "Stop Limit", "Trailing Stop" };

        #region portfolio
        this.listViewPositions.Columns.Add("Asset", 100);
        this.listViewPositions.Columns.Add("Price", 100);
        this.listViewPositions.Columns.Add("Quantity", 100);
        this.listViewPositions.Columns.Add("Market Value", 100);
        this.listViewPositions.Columns.Add("Total Profit", 100);

        this.listViewClosedOrders.Columns.Add("Asset", 100);
        this.listViewClosedOrders.Columns.Add("Order", 100);
        this.listViewClosedOrders.Columns.Add("Quantity", 100);
        this.listViewClosedOrders.Columns.Add("Average Cost", 100);
        this.listViewClosedOrders.Columns.Add("Amount", 100);
        this.listViewClosedOrders.Columns.Add("Status", 100);

        this.listViewOpenOrders.Columns.Add("Asset", 100);
        this.listViewOpenOrders.Columns.Add("Order", 100);
        this.listViewOpenOrders.Columns.Add("Quantity", 100);
        this.listViewOpenOrders.Columns.Add("Average Cost", 100);
        this.listViewOpenOrders.Columns.Add("Amount", 100);
        this.listViewOpenOrders.Columns.Add("Status", 100);
        this.listViewOpenOrders.Columns.Add("ClientId", 100);

        this.listViewWatchList.Columns.Add("Asset", 100);
        this.listViewWatchList.Columns.Add("BidSize", 100);
        this.listViewWatchList.Columns.Add("BidPrice", 100);
        this.listViewWatchList.Columns.Add("Last", 100);
        this.listViewWatchList.Columns.Add("AskPrice", 100);
        this.listViewWatchList.Columns.Add("AskSize", 100);
        #endregion
    }
    #endregion

    #region FormLoad
    /// <summary>
    /// initialization when the App loads
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void AlpacaDashboard_Load(object sender, EventArgs e)
    {
        var tn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone).ToString();
        _logger.LogInformation("AlpacaDashboard {0} at {1}", "Started", tn);

        //live
        if (_liveKey.Value.API_KEY != "")
        {
            LiveBroker = new Broker(token, _liveKey.Value.API_KEY, _liveKey.Value.API_SECRET, "Live", _mySettings, _logger);
            LivePfolioEnableEvents();
            await LiveBroker.Connect().ConfigureAwait(false);
            await LiveBroker.PositionAndOpenOrderAssets().ConfigureAwait(false);
        }

        //paper
        if (_paperKey.Value.API_KEY != "")
        {
            PaperBroker = new Broker(token, _paperKey.Value.API_KEY, _paperKey.Value.API_SECRET, "Paper", _mySettings, _logger);
            PaperPfolioEnableEvents();
            await PaperBroker.Connect().ConfigureAwait(false);
            await PaperBroker.PositionAndOpenOrderAssets().ConfigureAwait(false);
        }

        //assign broker to Stock class
        if (Environment == "Live")
            Stock.Broker = LiveBroker;
        if (Environment == "Paper")
            Stock.Broker = PaperBroker;

        #region bots auto generated control and events
        //add bot tabs for defined bot classes
        IEnumerable<Type> _bots = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(t => t.GetInterfaces().Contains(typeof(IBot)));
        foreach (Type _type in _bots)
        {
           
            TabPage tp = new TabPage(_type.Name);
            tabControlBots.TabPages.Add(tp);

            //get or create  watchlist in both environment
            IWatchList? wlLive = null;
            try
            {
                wlLive = await LiveBroker.GetWatchList(_type.Name);
            }
            catch (Exception)
            {
                wlLive = await LiveBroker.CreateWatchList(_type.Name, Array.Empty<string>());
            }

            IWatchList? wlPaper = null;
            try
            {
                wlPaper = await PaperBroker.GetWatchList(_type.Name);
            }
            catch (Exception)
            {
                wlPaper = await PaperBroker.CreateWatchList(_type.Name, Array.Empty<string>());
            }

            //add control dynamically to the scanner page
            Panel panel = new();
            panel.Dock = DockStyle.Fill;
            tp.Controls.Add(panel);
            SplitContainer splitContainer = new()
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };
            panel.Controls.Add(splitContainer);
            ListView listView = new()
            {
                Name = "listView" + _type.Name,
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            };

            //create instance of the bot class
            Dictionary<string, IBot> instances = new();
            IBot? instance = null;

            //live instance
            instance = (IBot?)Activator.CreateInstance(_type, new object[] { LiveBroker });

            if (instance != null)
            {
                instance.BotListUpdated += Instance_BotListUpdated;
                instance.UiContainer = splitContainer;
                instance.WatchList = wlLive;
                //load watch list symbol to instance
                if (wlLive != null)
                {
                    var assets = instance.WatchList.Assets;
                    instance.ListOfSymbolAndPosition = await LiveBroker.GetPositionsforAssetList(assets);
                    var symbols = instance.ListOfSymbolAndPosition.Select(x => x.Key).ToList();
                    await Stock.Subscribe(LiveBroker, symbols, "Bot");
                }
                instances.Add("Live", instance);
            }

            //paper instance
            instance = (IBot?)Activator.CreateInstance(_type, new object[] { PaperBroker });

            if (instance != null)
            {
                instance.BotListUpdated += Instance_BotListUpdated;
                instance.UiContainer = splitContainer;
                instance.WatchList = wlPaper;

                //load watch list symbol to instance
                if (wlPaper != null)
                {
                    var assets = instance.WatchList.Assets;
                    instance.ListOfSymbolAndPosition = await PaperBroker.GetPositionsforAssetList(assets);
                    var symbols = instance.ListOfSymbolAndPosition.Select(x => x.Key).ToList();
                    await Stock.Subscribe(PaperBroker, symbols, "Bot");
                }
                instances.Add("Paper", instance);
            }

            tp.Tag = instances;

            // add bot to contextmenu
            ToolStripMenuItem tsi = new("Add to " + _type.Name)
            {
                Tag = instances
            };
            contextMenuStripAddToBot.Items.Add(tsi);


            //used in ui event to load listview
            splitContainer.SplitterDistance = 440;
            splitContainer.Panel1.Controls.Add(listView);
            listView.Columns.Add("Asset", 100);
            listView.Columns.Add("Price", 100);
            listView.Columns.Add("Quantity", 100);
            listView.Columns.Add("Market Value", 100);
            listView.Columns.Add("Total Profit", 100);
            listView.MouseClick += BotList_MouseClick;
            TableLayoutPanel tableLayoutPanel = new()
            {
                Name = "tableLayoutPanel" + _type.Name,
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                AutoSize = true,
                AutoScroll = true
            };
            tableLayoutPanel.ColumnCount = 2;
            splitContainer.Panel2.Controls.Add(tableLayoutPanel);

            //get properties of the scannr class and assign defaul value form the instance to the UI textbox
            foreach (PropertyInfo p in _type.GetProperties())
            {
                if ((p.PropertyType == typeof(int) ||
                        p.PropertyType == typeof(decimal) ||
                        p.PropertyType == typeof(DateTime) ||
                        p.PropertyType == typeof(BarTimeFrameUnit) ||
                        p.PropertyType == typeof(BarTimeFrameUnit)
                    ))
                {
                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    Label lb = new()
                    {
                        Width = 120,
                        Text = p.Name
                    };
                    tableLayoutPanel.Controls.Add(lb);

                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                    if (instance != null)
                    {
                        GenerateControlForBot(_type, instance, tableLayoutPanel, p);
                    }
                    
                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                }
                tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            }
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

        }

        LoadBotDetails(Environment);

        #endregion

        #region scanners auto generated control and events
        //add scanner tabs for defined scanners classes
        IEnumerable<Type> _scanners = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(t => t.GetInterfaces().Contains(typeof(IScanner)));
        foreach (Type _type in _scanners)
        {
            TabPage tp = new TabPage(_type.Name);
            tabControlScanners.TabPages.Add(tp);

            //get or create  watchlist in both environment
            IWatchList? wlLive = null;
            try
            {
                wlLive = await LiveBroker.GetWatchList(_type.Name);
            }
            catch (Exception)
            {
                wlLive = await LiveBroker.CreateWatchList(_type.Name, Array.Empty<string>());
            }

            IWatchList? wlPaper = null;
            try
            {
                wlPaper = await PaperBroker.GetWatchList(_type.Name);
            }
            catch (Exception)
            {
                wlPaper = await PaperBroker.CreateWatchList(_type.Name, Array.Empty<string>());
            }

            //add control dynamically to the scanner page
            Panel panel = new()
            {
                Dock = DockStyle.Fill
            };
            tp.Controls.Add(panel);
            SplitContainer splitContainer = new SplitContainer();
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Orientation = Orientation.Vertical;
            panel.Controls.Add(splitContainer);
            ListView listView = new ListView();
            listView.Name = "listView" + _type.Name;
            listView.Dock = DockStyle.Fill;
            listView.View = View.Details;
            listView.FullRowSelect = true;

            //create instance of the scanner class
            Dictionary<string, IScanner> instances = new();
            IScanner? instance = null;

            //live instance
            instance = (IScanner?)Activator.CreateInstance(_type, new object[] { LiveBroker });

            if (instance != null)
            {
                instance.ScannerListUpdated += Instance_ScannerListUpdated;
                instance.UiContainer = splitContainer;
                instance.watchList = wlLive;
                //load watch list symbol to instance
                if (wlLive != null)
                {
                    var assets = instance.watchList.Assets;
                    instance.ListOfSymbolAndSnapshot = await LiveBroker.ListSnapShots(assets, 5000);
                    var symbols = instance.ListOfSymbolAndSnapshot.Select(x => x.Key).ToList();
                    await Stock.Subscribe(LiveBroker, symbols, "Scanner");
                }
                instances.Add("Live", instance);
            }

            //paper instance
            instance = (IScanner?)Activator.CreateInstance(_type, new object[] { PaperBroker });

            if (instance != null)
            {
                instance.ScannerListUpdated += Instance_ScannerListUpdated;
                instance.UiContainer = splitContainer;
                instance.watchList = wlPaper;
                //load watch list symbol to instance
                if (wlPaper != null)
                {
                    var assets = instance.watchList.Assets;
                    instance.ListOfSymbolAndSnapshot = await PaperBroker.ListSnapShots(assets, 5000);
                    var symbols = instance.ListOfSymbolAndSnapshot.Select(x => x.Key).ToList();
                    await Stock.Subscribe(PaperBroker, symbols, "Scanner");
                }
                instances.Add("Paper", instance);
            }
            
            tp.Tag = instances;

            //used in ui event to load listview
            splitContainer.SplitterDistance = 440;
            splitContainer.Panel1.Controls.Add(listView);
            listView.Columns.Add("Asset", 75);
            listView.Columns.Add("BidSize", 75);
            listView.Columns.Add("BidPrice", 75);
            listView.Columns.Add("Last", 75);
            listView.Columns.Add("AskPrice", 75);
            listView.Columns.Add("AskSize", 75);
            listView.MouseClick += WatchList_MouseClick;
            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.Name = "tableLayoutPanel" + _type.Name;
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            tableLayoutPanel.AutoSize = true;
            tableLayoutPanel.AutoScroll = true;
            tableLayoutPanel.ColumnCount = 2;
            splitContainer.Panel2.Controls.Add(tableLayoutPanel);

            //get properties of the scannr class and assign defaul value form the instance to the UI textbox
            foreach (PropertyInfo p in _type.GetProperties())
            {
                if ((p.PropertyType == typeof(int) ||
                        p.PropertyType == typeof(decimal) ||
                        p.PropertyType == typeof(DateTime) ||
                        p.PropertyType == typeof(BarTimeFrameUnit) ||
                        p.PropertyType == typeof(BarTimeFrameUnit)
                    ))
                {
                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    Label lb = new Label();
                    lb.Width = 120;
                    lb.Text = p.Name;
                    tableLayoutPanel.Controls.Add(lb);

                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                    if (instance != null)
                    {
                        GenerateControlForScanner(_type, instance, tableLayoutPanel, p);
                    }
                    
                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                }
                tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            }
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            Button btn = new()
            {
                Name = "textBox" + _type.Name + "Scan",
                Text = "Scan",
                BackColor = Color.Gray
            };
            tableLayoutPanel.Controls.Add(btn);
            btn.Click += ScanButton_Click;
        }
        LoadScannerDetails(Environment);

        #endregion

        //event to receive price updates
        Stock.PaperStockUpdated += PaperStock_StockUpdated;
        Stock.LiveStockUpdated += LiveStock_StockUpdated;

        //subscribe min bar for all symbol
        if (_mySettings.Value.Subscribed)
        {
            await Stock.SubscribeMinutesBarForAllSymbols(LiveBroker, PaperBroker).ConfigureAwait(false);
        }

        //start a event loop for portfolio and watchlist stock on periodic interval
        Stock.GenerateEvents(Environment, _mySettings.Value.PriceUpdateInterval, token);

        //update environment
        if (Environment == "Paper")
            await PaperBroker.UpdateEnviromentData();
        if (Environment == "Live")
            await LiveBroker.UpdateEnviromentData();

        //set up initial environment
        this.toolStripMenuItemPortfolio.PerformClick();

    }
    #endregion

    #region Event 

    /// <summary>
    /// Event handler for Paper environment
    /// </summary>
    /// <returns></returns>
    private void PaperPfolioEnableEvents()
    {
        //event to receive price updates

        //event to recive open order updates
        PaperBroker.OpenOrderUpdated += PaperPfolio_OpenOrderUpdated;
        //event to receive closed orders updates
        PaperBroker.ClosedOrderUpdated += PaperPfolio_ClosedOrderUpdated;
        //event to receive position updates
        PaperBroker.PositionUpdated += PaperPfolio_PositionUpdated;
        //event to receive account updates;
        PaperBroker.AccountUpdated += PaperPfolio_AccountUpdated;
    }

    /// <summary>
    /// Event handler for Live environment
    /// </summary>
    /// <returns></returns>
    private void LivePfolioEnableEvents()
    {
        //event to recive open order updates
        LiveBroker.OpenOrderUpdated += LivePfolio_OpenOrderUpdated;
        //event to receive closed orders updates
        LiveBroker.ClosedOrderUpdated += LivePfolio_ClosedOrderUpdated;
        //event to receive position updates
        LiveBroker.PositionUpdated += LivePfolio_PositionUpdated;
        //event to receive account updates;
        LiveBroker.AccountUpdated += LivePfolio_AccountUpdated;
    }

    #endregion

    #region Stock Price Update Logic

    /// <summary>
    /// event handler for Paper Stock data updated
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PaperStock_StockUpdated(object? sender, EventArgs e)
    {
        if (Environment == "Paper")
        {
            try
            {
                ListView? scannerListView = null;
                tabControlScanners.Invoke(new MethodInvoker(delegate ()
                {
                    var instances = (Dictionary<string, IScanner>)tabControlScanners.SelectedTab.Tag;
                    var instance = instances["Paper"];
                    var sc = (SplitContainer)instance.UiContainer;
                    scannerListView = (ListView)sc.Panel1.Controls[0];
                }));

                ListView? botListView = null;
                tabControlBots.Invoke(new MethodInvoker(delegate ()
                {
                    var instances = (Dictionary<string, IBot>)tabControlBots.SelectedTab.Tag;
                    var instance = instances["Paper"];
                    var sc = (SplitContainer)instance.UiContainer;
                    botListView = (ListView)sc.Panel1.Controls[0];
                }));

                IEnumerable<IStock>? stocks = ((StockUpdatedEventArgs)e).Stocks;

                if (stocks != null)
                {
                    foreach (Stock stock in stocks)
                    {
                        //update order box stock
                        UpdateOrderBoxPrices(stock);

                        //update portfolio position listview
                        UpdateListViewPositionsPrices(stock);

                        //update portfolio watchlist listview
                        UpdateListViewWatchListsQuote(listViewWatchList, stock);

                        //update scanner selected tab watchlist
                        if (scannerListView != null)
                            UpdateListViewWatchListsQuote(scannerListView, stock);

                        //update bot selected tab positions
                        if (botListView != null)
                            UpdateListViewBotPrices(botListView, stock);
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// event handler for Live Stock data updated
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LiveStock_StockUpdated(object? sender, EventArgs e)
    {
        if (Environment == "Live")
        {
            try
            {
                ListView? scannerListView = null;
                tabControlScanners.Invoke(new MethodInvoker(delegate ()
                {
                    var instances = (Dictionary<string, IScanner>)tabControlScanners.SelectedTab.Tag;
                    var instance = instances["Live"];
                    var sc = (SplitContainer)instance.UiContainer;
                    scannerListView = (ListView)sc.Panel1.Controls[0];
                }));

                ListView? botListView = null;
                tabControlBots.Invoke(new MethodInvoker(delegate ()
                {
                    var instances = (Dictionary<string, IBot>)tabControlBots.SelectedTab.Tag;
                    var instance = instances["Live"];
                    var sc = (SplitContainer)instance.UiContainer;
                    botListView = (ListView)sc.Panel1.Controls[0];
                }));

                IEnumerable<IStock>? stocks = ((StockUpdatedEventArgs)e).Stocks;

                if (stocks != null)
                {
                    foreach (Stock stock in stocks)
                    {
                        //update order box stock
                        UpdateOrderBoxPrices(stock);

                        //update portfolio position listview
                        UpdateListViewPositionsPrices(stock);

                        //update portfolio watchlist listview
                        UpdateListViewWatchListsQuote(listViewWatchList, stock);

                        //update scanner selected tab watchlist
                        if (scannerListView != null)
                            UpdateListViewWatchListsQuote(scannerListView, stock);

                        //update bot selected tab positions
                        if (botListView != null)
                            UpdateListViewBotPrices(botListView, stock);
                    }
                }
            }
            catch { }
        }
    }

    #endregion

    #region Account data display 
    /// <summary>
    /// event handler for Live account data
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LivePfolio_AccountUpdated(object? sender, EventArgs e)
    {
        IAccount? account = ((AccountUpdatedEventArgs)e).Account;

        if (account != null)
        {
            UpdateAccountLabel(account);
        }
    }

    /// <summary>
    /// event handler for paper account data
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PaperPfolio_AccountUpdated(object? sender, EventArgs e)
    {
        IAccount? account = ((AccountUpdatedEventArgs)e).Account;

        if (account != null)
        {
            UpdateAccountLabel(account);
        }
    }

    /// <summary>
    /// refresh account data in UI
    /// </summary>
    /// <param name="ac"></param>
    private void UpdateAccountLabel(IAccount ac)
    {
        try
        {
            labelAccountStatus.Invoke(new MethodInvoker(delegate ()
            {
                if (labelAccountStatus.Text != ac.Status.ToString()) labelAccountStatus.Text = ac.Status.ToString();
            }));
            labelAccountBuyingPower.Invoke(new MethodInvoker(delegate ()
            {
                if (labelAccountBuyingPower.Text != ac.Status.ToString()) labelAccountBuyingPower.Text = ac.BuyingPower.ToString();
            }));
            labelAccountEquity.Invoke(new MethodInvoker(delegate ()
            {
                if (labelAccountEquity.Text != ac.Status.ToString()) labelAccountEquity.Text = ac.Equity.ToString();
            }));
        }
        catch { }
    }
    #endregion

    #region Position data dsiplay

    /// <summary>
    /// event handler method to receive live position updates
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void LivePfolio_PositionUpdated(object? sender, EventArgs e)
    {
        IReadOnlyCollection<IPosition>? positions = ((PositionUpdatedEventArgs)e).Positions;

        if (positions != null)
        {
            UpdateListViewPositions("Live", positions);
        }
        
        var reqsymbol = await LiveBroker.PositionAndOpenOrderAssets().ConfigureAwait(false);
        LoadWatchListListView(listViewWatchList, reqsymbol);
    }

    /// <summary>
    /// event handler method to receive paper position updates
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void PaperPfolio_PositionUpdated(object? sender, EventArgs e)
    {
        IReadOnlyCollection<IPosition>? positions = ((PositionUpdatedEventArgs)e).Positions;

        if (positions != null)
        {
            UpdateListViewPositions("Paper", positions);
        }
        
        var reqsymbol = await PaperBroker.PositionAndOpenOrderAssets().ConfigureAwait(false);
        LoadWatchListListView(listViewWatchList, reqsymbol);
    }
    /// <summary>
    /// refresh position listview
    /// </summary>
    /// <param name="positions"></param>
    private void UpdateListViewPositions(string environment, IReadOnlyCollection<IPosition> positions)
    {
        listViewPositions.Invoke(new MethodInvoker(delegate () { listViewPositions.Items.Clear(); }));
        foreach (var pos in positions.ToList())
        {
            try
            {
                IStock? stock = null;
                if (environment == "Live" && Stock.LiveStockObjects != null)
                    stock = Stock.LiveStockObjects.GetStock(pos.Symbol);
                if (environment == "Paper" && Stock.PaperStockObjects != null)
                    stock = Stock.PaperStockObjects.GetStock(pos.Symbol);
                if (stock != null)
                {
                    stock.Qty = pos.Quantity;
                    stock.MarketValue = pos.MarketValue;
                    stock.OpenPositionValue = pos.CostBasis;
                }
                ListViewItem item = new ListViewItem(pos.Symbol);
                item.SubItems.Add(pos.AssetLastPrice.ToString());
                item.SubItems.Add(pos.Quantity.ToString());
                item.SubItems.Add(pos.MarketValue.ToString());
                item.SubItems.Add(pos.UnrealizedProfitLoss.ToString());
                listViewPositions.Invoke(new MethodInvoker(delegate () { listViewPositions.Items.Add(item); }));
            }
            catch { }
        }
    }
    /// <summary>
    /// refresh position listview with current prices
    /// </summary>
    /// <param name="stock"></param>
    private void UpdateListViewPositionsPrices(IStock stock)
    {
        try
        {
            //update position list prices
            listViewPositions.Invoke(new MethodInvoker(delegate ()
            {
                if (listViewPositions.Items.Count > 0)
                {
                    ListViewItem item = listViewPositions.FindItemWithText(stock.Asset.Symbol, false, 0, false);
                    if (item != null)
                    {
                        if (stock.Last.ToString() != "")
                        {
                            if (item.SubItems[1].Text != stock.Last.ToString()) item.SubItems[1].Text = stock.Last.ToString();
                            try
                            {
                                var marketValue = (Convert.ToDecimal(item.SubItems[1].Text) * Convert.ToDecimal(item.SubItems[2].Text));
                                if (item.SubItems[3].Text != marketValue.ToString()) item.SubItems[3].Text = marketValue.ToString();
                                if (stock.OpenPositionValue != null)
                                {
                                    var profit = marketValue - Convert.ToDecimal(stock.OpenPositionValue.ToString());
                                    if (item.SubItems[4].Text != profit.ToString()) item.SubItems[4].Text = profit.ToString();
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                }
            }));
        }
        catch { }
    }
    #endregion

    #region Open and Closed Orders
    /// <summary>
    /// event handler method to receive live closed order updates
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LivePfolio_ClosedOrderUpdated(object? sender, EventArgs e)
    {
        IReadOnlyCollection<IOrder>? closedOrders = ((ClosedOrderUpdatedEventArgs)e).ClosedOrders;

        if (closedOrders != null)
        {
            UpdateListViewClosedOrders(closedOrders);
        }
    }

    /// <summary>
    /// event handler method to receive paper closed order updates
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PaperPfolio_ClosedOrderUpdated(object? sender, EventArgs e)
    {
        IReadOnlyCollection<IOrder>? closedOrders = ((ClosedOrderUpdatedEventArgs)e).ClosedOrders;

        if (closedOrders != null)
        {
            UpdateListViewClosedOrders(closedOrders);
        }
    }

    /// <summary>
    /// event handler method to receive live open order updates
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void LivePfolio_OpenOrderUpdated(object? sender, EventArgs e)
    {
        IReadOnlyCollection<IOrder>? openOrders = ((OpenOrderUpdatedEventArgs)e).OpenOrders;

        if (openOrders != null)
        {
            UpdateListViewOpenOrders(openOrders);
        }
        
        var reqsymbol = await LiveBroker.PositionAndOpenOrderAssets().ConfigureAwait(false);
        LoadWatchListListView(listViewWatchList, reqsymbol);
    }

    /// <summary>
    /// event handler method to receive paper open order updates
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void PaperPfolio_OpenOrderUpdated(object? sender, EventArgs e)
    {
        IReadOnlyCollection<IOrder>? openOrders = ((OpenOrderUpdatedEventArgs)e).OpenOrders;

        if (openOrders != null)
        {
            UpdateListViewOpenOrders(openOrders);
        }
        
        var reqsymbol = await PaperBroker.PositionAndOpenOrderAssets().ConfigureAwait(false);
        LoadWatchListListView(listViewWatchList, reqsymbol);
    }
    /// <summary>
    /// refresh closed order listview
    /// </summary>
    /// <param name="closedOrders"></param>
    private void UpdateListViewClosedOrders(IReadOnlyCollection<IOrder> closedOrders)
    {
        try
        {
            listViewClosedOrders.Invoke(new MethodInvoker(delegate () { listViewClosedOrders.Items.Clear(); }));
            foreach (var co in closedOrders.ToList())
            {
                ListViewItem item = new(co.Symbol);
                var tz = co.FilledAtUtc == null ? "" : TimeZoneInfo.ConvertTimeFromUtc((DateTime)co.FilledAtUtc, easternZone).ToString();
                var ord = $"{co.OrderType} { co.OrderSide} @ {co.LimitPrice} {tz}";
                item.SubItems.Add(ord);
                item.SubItems.Add(co.FilledQuantity.ToString());
                item.SubItems.Add(co.AverageFillPrice.ToString());
                item.SubItems.Add((co.Quantity * co.LimitPrice).ToString());
                item.SubItems.Add(co.OrderStatus.ToString());
                listViewClosedOrders.Invoke(new MethodInvoker(delegate () { listViewClosedOrders.Items.Add(item); }));
            }
        }
        catch { }
    }

    /// <summary>
    /// refresh open order listview
    /// </summary>
    /// <param name="openOrders"></param>
    private void UpdateListViewOpenOrders(IReadOnlyCollection<IOrder> openOrders)
    {
        try
        {
            listViewOpenOrders.Invoke(new MethodInvoker(delegate () { listViewOpenOrders.Items.Clear(); }));
            foreach (var oo in openOrders.ToList())
            {
                ListViewItem item = new ListViewItem(oo.Symbol);
                var tz = oo.FilledAtUtc == null ? "" : TimeZoneInfo.ConvertTimeFromUtc((DateTime)oo.FilledAtUtc, easternZone).ToString();
                var ord = $"{oo.OrderType.ToString()} { oo.OrderSide.ToString()} @ {oo.LimitPrice.ToString()} {tz}";
                item.SubItems.Add(ord);
                item.SubItems.Add(oo.FilledQuantity.ToString());
                item.SubItems.Add(oo.AverageFillPrice.ToString());
                item.SubItems.Add((oo.Quantity * oo.LimitPrice).ToString());
                item.SubItems.Add(oo.OrderStatus.ToString());
                item.SubItems.Add(oo.OrderId.ToString());
                listViewOpenOrders.Invoke(new MethodInvoker(delegate () { listViewOpenOrders.Items.Add(item); }));
            }
        }
        catch { }
    }
    #endregion

    #region Listviews methods
    /// <summary>
    /// refresh any watchlist listview with assets
    /// </summary>
    /// <param name="lv"></param>
    /// <param name="listOfSymbolSnapShot"></param>
    private void LoadWatchListListView(ListView lv, Dictionary<string, ISnapshot> listOfSymbolSnapShot)
    {
        try
        {
            if (listOfSymbolSnapShot != null)
            {
                lv.Invoke(new MethodInvoker(delegate () { lv.Items.Clear(); }));
                foreach (var assetSnapShot in listOfSymbolSnapShot.ToList())
                {
                    ListViewItem item = new ListViewItem(assetSnapShot.Key);
                    try
                    {
                        if (assetSnapShot.Value != null && assetSnapShot.Value.Quote != null && assetSnapShot.Value.CurrentDailyBar != null)
                        {
                            item.SubItems.Add(assetSnapShot.Value.Quote.BidSize.ToString());
                            item.SubItems.Add(assetSnapShot.Value.Quote.BidPrice.ToString());
                            item.SubItems.Add(assetSnapShot.Value.CurrentDailyBar.Close.ToString());
                            item.SubItems.Add(assetSnapShot.Value.Quote.AskPrice.ToString());
                            item.SubItems.Add(assetSnapShot.Value.Quote.AskSize.ToString());
                            lv.Invoke(new MethodInvoker(delegate () { lv.Items.Add(item); }));
                        }
                    }
                    catch { }
                }
            }
            else
            {
                lv.Invoke(new MethodInvoker(delegate () { lv.Items.Clear(); }));
            }
        }
        catch { }
    }

    /// <summary>
    /// refresh any watchlist listview with bid and ask related data
    /// </summary>
    /// <param name="lv"></param>
    /// <param name="stock"></param>
    private void UpdateListViewWatchListsQuote(ListView lv, IStock stock)
    {
        try
        {
            //update watch list prices
            lv.Invoke(new MethodInvoker(delegate ()
            {
                if (lv.Items.Count > 0)
                {
                    ListViewItem item = lv.FindItemWithText(stock.Asset.Symbol, false, 0, false);
                    if (item != null)
                    {
                        //if (stock.subscribed) item.BackColor = Color.SkyBlue;
                        if (stock.BidSize != null && item.SubItems[1].Text != stock.BidSize.ToString()) item.SubItems[1].Text = stock.BidSize.ToString();
                        if (stock.BidPrice != null && item.SubItems[2].Text != stock.BidPrice.ToString()) item.SubItems[2].Text = stock.BidPrice.ToString();
                        if (stock.Last != null && item.SubItems[3].Text != stock.Last.ToString()) item.SubItems[3].Text = stock.Last.ToString();
                        if (stock.AskPrice != null && item.SubItems[4].Text != stock.AskPrice.ToString()) item.SubItems[4].Text = stock.AskPrice.ToString();
                        if (stock.AskSize != null && item.SubItems[5].Text != stock.AskSize.ToString()) item.SubItems[5].Text = stock.AskSize.ToString();
                    }
                }
            }));
        }
        catch { }
    }

    /// <summary>
    /// handle logic when position listview item is clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void listViewPositions_MouseClick(object sender, MouseEventArgs e)
    {
        try
        {
            //row hit
            Point mousePosition = listViewPositions.PointToClient(Control.MousePosition);
            ListViewHitTestInfo hit = listViewPositions.HitTest(mousePosition);
            if (hit.SubItem != null)
            {
                int columnindex = hit.Item.SubItems.IndexOf(hit.SubItem);
                var focusedItem = listViewPositions.FocusedItem;

                radioButtonShares.Checked = true;
                //default to sell
                radioButtonSell.Checked = true;
                //symbol
                textBoxSymbol.Text = focusedItem.SubItems[0].Text;

                //show context menu 
                if (e.Button == MouseButtons.Right)
                {
                    if (focusedItem != null && focusedItem.Bounds.Contains(e.Location))
                    {
                        contextMenuStripAddToBot.Tag = focusedItem.SubItems[0];
                        contextMenuStripAddToBot.Show(Cursor.Position);
                    }
                }
            }

        }
        catch { }
    }

    /// <summary>
    /// handle logic when watchlist listview item is clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void WatchList_MouseClick(object? sender, MouseEventArgs e)
    {
        //row hit
        ListView? lv = (ListView?)sender;

        if (lv != null)
        {
            Point mousePosition = lv.PointToClient(Control.MousePosition);
            ListViewHitTestInfo hit = lv.HitTest(mousePosition);
            int columnindex = hit.Item.SubItems.IndexOf(hit.SubItem);
            var focusedItem = lv.FocusedItem;

            if (hit.SubItem != null)
            {
                //sell or buy based position
                var symbol = focusedItem.SubItems[0].Text;
                IPosition? position = null;
                if (Environment == "Live")
                {
                    position = await LiveBroker.GetCurrentPosition(symbol);
                }
                if (Environment == "Paper")
                {
                    position = await PaperBroker.GetCurrentPosition(symbol);
                }
                if (position != null && position.Quantity > 0)
                {
                    radioButtonSell.Checked = true;
                }
                else if (position != null && position.Quantity < 0)
                {
                    radioButtonBuy.Checked = true;
                }
                else
                {
                    radioButtonBuy.Checked = true;
                }
                if (columnindex == 1 || columnindex == 2)
                {
                    labelMarketPrice.Text = focusedItem.SubItems[2].Text;
                    textBoxLimitPrice.Text = focusedItem.SubItems[2].Text;
                }
                if (columnindex == 3)
                {
                    labelMarketPrice.Text = focusedItem.SubItems[3].Text;
                    textBoxLimitPrice.Text = focusedItem.SubItems[3].Text;
                }
                if (columnindex == 4 || columnindex == 5)
                {
                    labelMarketPrice.Text = focusedItem.SubItems[4].Text;
                    textBoxLimitPrice.Text = focusedItem.SubItems[4].Text;
                }
                //symbol
                radioButtonShares.Checked = true;
                textBoxSymbol.Text = focusedItem.SubItems[0].Text;
                SetEstimatedPriceOrQuantity(focusedItem);

                //show context menu 
                if (e.Button == MouseButtons.Right)
                {
                    if (focusedItem != null && focusedItem.Bounds.Contains(e.Location))
                    {
                        contextMenuStripAddToBot.Tag = focusedItem.SubItems[0];
                        contextMenuStripAddToBot.Show(Cursor.Position);
                    }
                }
            }
        }
    }

    /// <summary>
    /// handle logic when open order listview item is clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void listViewOpenOrders_MouseClick(object sender, MouseEventArgs e)
    {
        try
        {
            //row hit
            Point mousePosition = listViewOpenOrders.PointToClient(Control.MousePosition);
            ListViewHitTestInfo hit = listViewOpenOrders.HitTest(mousePosition);
            int columnindex = hit.Item.SubItems.IndexOf(hit.SubItem);
            var focusedItem = listViewOpenOrders.FocusedItem;
            if (e.Button == MouseButtons.Right)
            {
                if (focusedItem != null && focusedItem.Bounds.Contains(e.Location))
                {
                    contextMenuStripOpenOrder.Show(Cursor.Position);
                }
            }
        }
        catch { }
    }
    #endregion

    #region Main Window UI events handlers

    /// <summary>
    /// Portfoilo menu clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ToolStripMenuItemPortfolio_Click(object sender, EventArgs e)
    {
        tabControlPortfolio.Visible = true;
        tabControlBots.Visible = false;
        tabControlScanners.Visible = false;
        panelOrder.Visible = true;
        this.textBoxLimitPrice.Visible = false;
        this.labelLimitPrice.Visible = false;
        this.textBoxLimitPrice.Text = "0.00";
    }

    /// <summary>
    /// Bots menu clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ToolStripMenuItemBot_Click(object sender, EventArgs e)
    {
        tabControlPortfolio.Visible = false;
        tabControlBots.Visible = true;
        tabControlScanners.Visible = false;
        panelOrder.Visible = false;
    }

    /// <summary>
    /// Scanners menu clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ToolStripMenuItemScanners_Click(object sender, EventArgs e)
    {
        tabControlPortfolio.Visible = false;
        tabControlBots.Visible = false;
        tabControlScanners.Visible = true;
        panelOrder.Visible = true;
    }

    /// <summary>
    /// App closing
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AlpacaDashboard_FormClosing(object sender, FormClosingEventArgs e)
    {
        var tn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone).ToString();
        _logger.LogInformation("AlpacaDashboard {0} at {1}", "Ended", tn);
    }

    /// <summary>
    /// Live or Paper environment checkbox checked event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void CheckBoxLivePaper_CheckedChanged(object sender, EventArgs e)
    {
        if (((CheckBox)sender).Checked)
        {
            Environment = "Live";
            Stock.Broker = LiveBroker;
            await LiveBroker.UpdateEnviromentData().ConfigureAwait(false);
        }
        else
        {
            Environment = "Paper";
            Stock.Broker = PaperBroker;
            await PaperBroker.UpdateEnviromentData().ConfigureAwait(false);
        }

        LoadScannerDetails(Environment);
        LoadBotDetails(Environment);
    }

    #endregion

    #region Order box event handlers and methods

    /// <summary>
    /// Update the prices in the Order box area
    /// </summary>
    /// <param name="Stock"></param>
    private void UpdateOrderBoxPrices(IStock Stock)
    {
        if (Stock.Asset.Symbol == textBoxSymbol.Text)
        {
            labelBidPrice.Invoke(new MethodInvoker(delegate ()
            {
                if (Stock.BidPrice != null && labelBidPrice.Text != Stock.BidPrice.ToString()) labelBidPrice.Text = Stock.BidPrice.ToString();
            }));
            labelMarketPrice.Invoke(new MethodInvoker(delegate ()
            {
                if (Stock.Last != null && labelMarketPrice.Text != Stock.Close.ToString()) labelMarketPrice.Text = Stock.Last.ToString();
            }));
            labelAskPrice.Invoke(new MethodInvoker(delegate ()
            {
                if (Stock.AskPrice != null && labelAskPrice.Text != Stock.AskPrice.ToString()) labelAskPrice.Text = Stock.AskPrice.ToString();
            }));
        }
    }

    private void textBoxSymbol_KeyPress(object sender, KeyPressEventArgs e)
    {
        e.KeyChar = Char.ToUpper(e.KeyChar);
    }
    private void textBoxLimitPrice_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
        {
            e.Handled = true;
        }
    }
    private void textBoxStopPrice_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
        {
            e.Handled = true;
        }
    }
    private void textBoxAmount_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
        {
            e.Handled = true;
        }
    }
    private void textBoxTrailRateOrPrice_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
        {
            e.Handled = true;
        }
    }
    private void textBoxSymbol_Leave(object sender, EventArgs e)
    {

    }
    private void TextBoxQuantity_KeyPress(object sender, KeyPressEventArgs e)
    {
        IAsset? asset = null;

        if (textBoxSymbol.Tag != null)
        {
            object[] objs = (object[])textBoxSymbol.Tag;
            asset = (IAsset)objs[0];
        }
        if (asset != null)
        {
            if (asset.Class == AssetClass.UsEquity)
            {
                if (comboBoxMarketOrLimit.Text != "Market")
                {
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    {
                        e.Handled = true;
                    }
                }
                else
                {
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
                    {
                        e.Handled = true;
                    }
                }
            }
            if (asset.Class == AssetClass.Crypto)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
                {
                    e.Handled = true;
                }
            }
        }
    }
    private void LabelBidPrice_Click(object sender, EventArgs e)
    {
        textBoxLimitPrice.Text = labelBidPrice.Text;
    }

    private void LabelMarketPrice_Click(object sender, EventArgs e)
    {
        textBoxLimitPrice.Text = labelMarketPrice.Text;
    }

    private void LabelAskPrice_Click(object sender, EventArgs e)
    {
        textBoxLimitPrice.Text = labelAskPrice.Text;
    }
    /// <summary>
    /// order symbo, changed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void textBoxSymbol_TextChanged(object sender, EventArgs e)
    {
        ISnapshot? snapshot = null;
        IAsset? asset = null;
        IPosition? position = null;

        if (textBoxSymbol.Text != "")
        {
            if (Environment == "Live")
            {
                try
                {
                    snapshot = await LiveBroker.GetSnapshot(textBoxSymbol.Text).ConfigureAwait(false);
                    asset = await LiveBroker.GetAsset(textBoxSymbol.Text).ConfigureAwait(false);
                    position = await LiveBroker.GetCurrentPosition(textBoxSymbol.Text).ConfigureAwait(false);
                }
                catch { }
            }
            if (Environment == "Paper")
            {
                try
                {
                    snapshot = await PaperBroker.GetSnapshot(textBoxSymbol.Text).ConfigureAwait(false);
                    asset = await PaperBroker.GetAsset(textBoxSymbol.Text).ConfigureAwait(false);
                    position = await PaperBroker.GetCurrentPosition(textBoxSymbol.Text).ConfigureAwait(false);
                }
                catch { }
            }

            var qty = position == null ? 0 : position.Quantity;
            var price = snapshot == null || snapshot.Trade == null ? 0 : snapshot.Trade.Price;
            var bidPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.BidPrice;
            var askPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.AskPrice;

            if (asset != null && snapshot != null && position != null)
            {
                textBoxSymbol.Tag = new object[] { asset, snapshot, position };
            }

            radioButtonBuy.Checked = qty <= 0 ? true : false;
            radioButtonSell.Checked = qty > 0 ? true : false;
            textBoxAmount.Visible = false;
            textBoxQuantity.Visible = true;

            labelMarketPrice.Text = price.ToString();
            textBoxLimitPrice.Text = price.ToString();
            textBoxStopPrice.Text = bidPrice.ToString();

            textBoxQuantity.Text = qty.ToString();
            labelEstimatedPriceOrQuantityValue.Text = (qty * price).ToString();

            if (radioButtonShares.Checked)
            {
                textBoxQuantity.Text = qty.ToString();
                labelEstimatedPriceOrQuantityValue.Text = (qty * price).ToString();
            }
            else
            {
                textBoxQuantity.Text = price.ToString();
                labelEstimatedPriceOrQuantityValue.Text = qty.ToString();
            }

            if (asset != null)
            {
                var si = comboBoxMarketOrLimit.SelectedItem;
                if (asset.Class == AssetClass.UsEquity)
                    comboBoxMarketOrLimit.DataSource = new List<string>() { "Market", "Limit", "Stop", "Stop Limit", "Trailing Stop" };
                else
                    comboBoxMarketOrLimit.DataSource = new List<string>() { "Market", "Limit", "Stop Limit" };
                comboBoxMarketOrLimit.SelectedItem = si;
            }

            if (asset != null && textBoxSymbol.Text != "")
            {
                if (Environment == "Live")
                {
                    await Stock.Subscribe(LiveBroker, textBoxSymbol.Text, "Order").ConfigureAwait(false);
                }
                if (Environment == "Paper")
                {
                    await Stock.Subscribe(PaperBroker, textBoxSymbol.Text, "Order").ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Order screen Share radio box selected event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void RadioButtonShares_Click(object sender, EventArgs e)
    {
        RadioButtonSharedClicked();
    }

    /// <summary>
    /// Radio button shares clicked logic        
    /// </summary>
    private void RadioButtonSharedClicked()
    {
        ISnapshot? snapshot = null;
        IPosition? position = null;

        if (textBoxSymbol.Tag != null)
        {
            object[] objs = (object[])textBoxSymbol.Tag;
            snapshot = (ISnapshot)objs[1];
            position = (IPosition)objs[2];
        }
        var qty = position == null ? 0 : position.Quantity;
        var price = snapshot == null || snapshot.Trade == null ? 0 : snapshot.Trade.Price;
        var bidPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.BidPrice;
        var askPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.AskPrice;

        var tbq = textBoxQuantity.Text == "" ? 1 : Convert.ToDecimal(textBoxQuantity.Text);
        var tba = textBoxAmount.Text == "" ? 1 : Convert.ToDecimal(textBoxAmount.Text);
        var tblp = textBoxLimitPrice.Text == "" ? 1 : Convert.ToDecimal(textBoxLimitPrice.Text);
        var tbmp = labelMarketPrice.Text == "" ? 1 : Convert.ToDecimal(labelMarketPrice.Text);
        var lepq = labelEstimatedPriceOrQuantityValue.Text == "" ? 1 : Convert.ToDecimal(labelEstimatedPriceOrQuantityValue.Text);
        var tbp = comboBoxMarketOrLimit.Text == "Market" ? tbmp : tblp;

        textBoxQuantity.Text = qty.ToString();
        labelEstimatedPriceOrQuantityValue.Text = (qty * tbp).ToString();

        labelShareOrAmount.Text = "Quantity";
        labelEstimatedPriceOrQuantity.Text = "Estimated Amount";
        textBoxAmount.Visible = false;
        textBoxQuantity.Visible = true;
    }

    /// <summary>
    /// Order screen Dollar radio box selected event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void radioButtonDollars_Click(object sender, EventArgs e)
    {
        RadioButtonDollarsCliked();
    }

    /// <summary>
    /// Radio button dollars clicked logic        
    /// </summary>
    private void RadioButtonDollarsCliked()
    {
        ISnapshot? snapshot = null;
        IPosition? position = null;

        if (textBoxSymbol.Tag != null)
        {
            object[] objs = (object[])textBoxSymbol.Tag;
            snapshot = (ISnapshot)objs[1];
            position = (IPosition)objs[2];
        }

        var qty = position == null ? 0 : position.Quantity;
        var price = snapshot == null || snapshot.Trade == null ? 0 : snapshot.Trade.Price;
        var bidPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.BidPrice;
        var askPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.AskPrice;

        var tbq = textBoxQuantity.Text == "" ? 1 : Convert.ToDecimal(textBoxQuantity.Text);
        var tba = textBoxAmount.Text == "" ? 1 : Convert.ToDecimal(textBoxAmount.Text);
        var tblp = textBoxLimitPrice.Text == "" ? 1 : Convert.ToDecimal(textBoxLimitPrice.Text);
        var tbmp = labelMarketPrice.Text == "" ? 1 : Convert.ToDecimal(labelMarketPrice.Text);
        var lepq = labelEstimatedPriceOrQuantityValue.Text == "" ? 1 : Convert.ToDecimal(labelEstimatedPriceOrQuantityValue.Text);
        var tbp = comboBoxMarketOrLimit.Text == "Market" ? tbmp : tblp;

        textBoxAmount.Text = (qty * tbp).ToString();
        try
        {
            labelEstimatedPriceOrQuantityValue.Text = qty.ToString();
        }
        catch { }

        labelShareOrAmount.Text = "Amount";
        labelEstimatedPriceOrQuantity.Text = "Estimated Quantity";
        textBoxAmount.Visible = true;
        textBoxQuantity.Visible = false;
    }

    /// <summary>
    /// Order screen logic for quantity  changes
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void textBoxQuantity_TextChanged(object sender, EventArgs e)
    {
        ISnapshot? snapshot = null;
        IPosition? position = null;

        if (textBoxSymbol.Tag != null)
        {
            object[] objs = (object[])textBoxSymbol.Tag;
            snapshot = (ISnapshot)objs[1];
            position = (IPosition)objs[2];
        }

        //Estimated Price or Quantity
        var qty = position == null ? 0 : position.Quantity;
        var price = snapshot == null || snapshot.Trade == null ? 0 : snapshot.Trade.Price;
        var bidPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.BidPrice;
        var askPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.AskPrice;

        var tbq = textBoxQuantity.Text == "" ? 1 : Convert.ToDecimal(textBoxQuantity.Text);
        var tba = textBoxAmount.Text == "" ? 1 : Convert.ToDecimal(textBoxAmount.Text);
        var tblp = textBoxLimitPrice.Text == "" ? 1 : Convert.ToDecimal(textBoxLimitPrice.Text);
        var tbmp = labelMarketPrice.Text == "" ? 1 : Convert.ToDecimal(labelMarketPrice.Text);
        var lepq = labelEstimatedPriceOrQuantityValue.Text == "" ? 1 : Convert.ToDecimal(labelEstimatedPriceOrQuantityValue.Text);
        var tbp = comboBoxMarketOrLimit.Text == "Market" ? tbmp : tblp;

        labelEstimatedPriceOrQuantityValue.Text = (tbq * tbp).ToString();

        if (qty > 0 && tbq > qty)
            buttonConfirm.Enabled = false;
        else
            buttonConfirm.Enabled = true;
    }

    /// <summary>
    /// Order screen logic for amount  changes
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void textBoxAmount_TextChanged(object sender, EventArgs e)
    {
        ISnapshot? snapshot = null;
        IPosition? position = null;

        if (textBoxSymbol.Tag != null)
        {
            object[] objs = (object[])textBoxSymbol.Tag;
            snapshot = (ISnapshot)objs[1];
            position = (IPosition)objs[2];
        }

        //Estimated Price or Quantity
        var qty = position == null ? 0 : position.Quantity;
        var price = snapshot == null || snapshot.Trade == null ? 0 : snapshot.Trade.Price;
        var bidPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.BidPrice;
        var askPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.AskPrice;

        var tbq = textBoxQuantity.Text == "" ? 1 : Convert.ToDecimal(textBoxQuantity.Text);
        var tba = textBoxAmount.Text == "" ? 1 : Convert.ToDecimal(textBoxAmount.Text);
        var tblp = textBoxLimitPrice.Text == "" ? 1 : Convert.ToDecimal(textBoxLimitPrice.Text);
        var tbmp = labelMarketPrice.Text == "" ? 1 : Convert.ToDecimal(labelMarketPrice.Text);
        var lepq = labelEstimatedPriceOrQuantityValue.Text == "" ? 1 : Convert.ToDecimal(labelEstimatedPriceOrQuantityValue.Text);
        var tbp = comboBoxMarketOrLimit.Text == "Market" ? tbmp : tblp;

        try
        {
            labelEstimatedPriceOrQuantityValue.Text = (tba / tbp).ToString();
        }
        catch { }

        if (qty > 0 && tba / tbp > qty)
            buttonConfirm.Enabled = false;
        else
            buttonConfirm.Enabled = true;

    }

    /// <summary>
    /// Order screen logig when limit price changes
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TextBoxLimitPrice_TextChanged(object sender, EventArgs e)
    {
        ISnapshot? snapshot = null;
        IPosition? position = null;
        if (textBoxSymbol.Tag != null)
        {
            object[] objs = (object[])textBoxSymbol.Tag;
            snapshot = (ISnapshot)objs[1];
            position = (IPosition)objs[2];
        }

        var qty = position == null ? 0 : position.Quantity;
        var price = snapshot == null || snapshot.Trade == null ? 0 : snapshot.Trade.Price;
        var bidPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.BidPrice;
        var askPrice = snapshot == null || snapshot.Quote == null ? 0 : snapshot.Quote.AskPrice;

        var tbq = textBoxQuantity.Text == "" ? 1 : Convert.ToDecimal(textBoxQuantity.Text);
        var tba = textBoxAmount.Text == "" ? 1 : Convert.ToDecimal(textBoxAmount.Text);
        var tblp = textBoxLimitPrice.Text == "" ? 1 : Convert.ToDecimal(textBoxLimitPrice.Text);
        var tbmp = labelMarketPrice.Text == "" ? 1 : Convert.ToDecimal(labelMarketPrice.Text);
        var lepq = labelEstimatedPriceOrQuantityValue.Text == "" ? 1 : Convert.ToDecimal(labelEstimatedPriceOrQuantityValue.Text);
        var tbp = comboBoxMarketOrLimit.Text == "Market" ? tbmp : tblp;

        labelEstimatedPriceOrQuantityValue.Text = (tbq * tbp).ToString();

        if (qty > 0 && tbq > qty)
            buttonConfirm.Enabled = false;
        else
            buttonConfirm.Enabled = true;

    }

    /// <summary>
    /// Order screen Market or Limit order selected event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ComboBoxMarketOrLimit_SelectedIndexChanged(object sender, EventArgs e)
    {
        IAsset? asset = null;
        if (textBoxSymbol.Tag != null)
        {
            object[] objs = (object[])textBoxSymbol.Tag;
            asset = (IAsset)objs[0];
        }
        if (comboBoxMarketOrLimit.Text == "Market")
        {
            if (asset != null)
            {
                if (asset.Class == AssetClass.UsEquity)
                    comboBoxTimeInForce.DataSource = new List<string>() { "DAY", "DAYE", "GTC", "FOK", "IOC", "OPG", "CLS" };
                else
                    comboBoxTimeInForce.DataSource = new List<string>() { };
            }
            groupBoxSharesOrDollar.Visible = true;
            groupBoxTrailRatePrice.Visible = false;
            labelTimeInForce.Visible = false;
            comboBoxTimeInForce.Visible = false;
            textBoxLimitPrice.Visible = false;
            labelStopPrice.Visible = false;
            textBoxStopPrice.Visible = false;
            labelLimitPrice.Visible = false;
            textBoxTrailRateOrPrice.Visible = false;
            RadioButtonSharedClicked();
        }
        if (comboBoxMarketOrLimit.Text == "Limit")
        {
            if (asset != null)
            {
                if (asset.Class == AssetClass.UsEquity)
                    comboBoxTimeInForce.DataSource = new List<string>() { "DAY", "DAYE", "GTC", "FOK", "IOC", "OPG", "CLS" };
                else
                    comboBoxTimeInForce.DataSource = new List<string>() { "GTC", "FOK", "IOC" };
            }
            groupBoxSharesOrDollar.Visible = false;
            groupBoxTrailRatePrice.Visible = false;
            labelTimeInForce.Visible = true;
            comboBoxTimeInForce.Visible = true;
            radioButtonShares.Checked = true;
            labelStopPrice.Visible = false;
            textBoxStopPrice.Visible = false;
            labelLimitPrice.Visible = true;
            textBoxLimitPrice.Visible = true;
            textBoxTrailRateOrPrice.Visible = false;
            RadioButtonSharedClicked();

        }
        if (comboBoxMarketOrLimit.Text == "Stop")
        {
            if (asset != null)
            {
                if (asset.Class == AssetClass.UsEquity)
                    comboBoxTimeInForce.DataSource = new List<string>() { "DAY", "GTC", "FOK", "IOC", "OPG", "CLS" };
                else
                    comboBoxTimeInForce.DataSource = new List<string>() { "GTC", "FOK", "IOC" };
            }
            groupBoxSharesOrDollar.Visible = false;
            groupBoxTrailRatePrice.Visible = false;
            labelTimeInForce.Visible = true;
            comboBoxTimeInForce.Visible = true;
            radioButtonShares.Checked = true;
            labelStopPrice.Visible = true;
            textBoxStopPrice.Visible = true;
            labelLimitPrice.Visible = false;
            textBoxLimitPrice.Visible = false;
            textBoxTrailRateOrPrice.Visible = false;
            RadioButtonSharedClicked();

        }
        if (comboBoxMarketOrLimit.Text == "Stop Limit")
        {
            if (asset != null)
            {
                if (asset.Class == AssetClass.UsEquity)
                    comboBoxTimeInForce.DataSource = new List<string>() { "DAY", "GTC", "FOK", "IOC", "OPG", "CLS" };
                else
                    comboBoxTimeInForce.DataSource = new List<string>() { "GTC", "FOK", "IOC" };
            }
            groupBoxSharesOrDollar.Visible = false;
            groupBoxTrailRatePrice.Visible = false;
            labelTimeInForce.Visible = true;
            comboBoxTimeInForce.Visible = true;
            radioButtonShares.Checked = true;
            labelStopPrice.Visible = true;
            textBoxStopPrice.Visible = true;
            labelLimitPrice.Visible = true;
            textBoxLimitPrice.Visible = true;
            textBoxTrailRateOrPrice.Visible = false;
            RadioButtonSharedClicked();

        }
        if (comboBoxMarketOrLimit.Text == "Trailing Stop")
        {
            if (asset != null)
            {
                if (asset.Class == AssetClass.UsEquity)
                    comboBoxTimeInForce.DataSource = new List<string>() { "DAY", "GTC" };
                else
                    comboBoxTimeInForce.DataSource = new List<string>() { };
            }
            groupBoxSharesOrDollar.Visible = false;
            groupBoxTrailRatePrice.Visible = true;
            labelTimeInForce.Visible = true;
            comboBoxTimeInForce.Visible = true;
            radioButtonShares.Checked = true;
            labelStopPrice.Visible = true;
            textBoxStopPrice.Visible = true;
            labelLimitPrice.Visible = false;
            textBoxLimitPrice.Visible = false;
            textBoxTrailRateOrPrice.Visible = true;
            RadioButtonSharedClicked();

        }
    }

    /// <summary>
    /// Order screen calculate estimated price or quantity
    /// </summary>
    /// <param name="focusedItem"></param>
    private void SetEstimatedPriceOrQuantity(ListViewItem focusedItem)
    {
        //Estimated Price or Quantity
        if (radioButtonShares.Checked)
        {
            textBoxQuantity.Text = "1";
            try
            {
                var ep = Convert.ToDecimal(textBoxQuantity.Text) * Convert.ToDecimal(labelMarketPrice.Text);
                labelEstimatedPriceOrQuantityValue.Text = ep.ToString();
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// Handle Order submition logic
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void buttonOrderConfirm_Click(object sender, EventArgs e)
    {
        decimal qty = Convert.ToDecimal(textBoxQuantity.Text);
        decimal amt = Convert.ToDecimal(textBoxAmount.Text);
        decimal limitPrice = Convert.ToDecimal(textBoxLimitPrice.Text);
        decimal stopPrice = Convert.ToDecimal(textBoxStopPrice.Text);
        decimal trialingRateOrPrice = Convert.ToDecimal(textBoxTrailRateOrPrice.Text);

        //buy or sell
        OrderSide orderSide = OrderSide.Buy;
        if (radioButtonSell.Checked)
        {
            orderSide = OrderSide.Sell;
        }
        if (radioButtonBuy.Checked)
        {
            orderSide = OrderSide.Buy;
        }

        //time in force
        bool extendedHours = false;
        TimeInForce timeInForce = TimeInForce.Day;
        if (comboBoxTimeInForce.Text == "DAY") timeInForce = TimeInForce.Day;
        if (comboBoxTimeInForce.Text == "GTC") timeInForce = TimeInForce.Gtc;
        if (comboBoxTimeInForce.Text == "FOK") timeInForce = TimeInForce.Fok;
        if (comboBoxTimeInForce.Text == "IOC") timeInForce = TimeInForce.Ioc;
        if (comboBoxTimeInForce.Text == "OPG") timeInForce = TimeInForce.Opg;
        if (comboBoxTimeInForce.Text == "CLS") timeInForce = TimeInForce.Cls;
        if (comboBoxTimeInForce.Text == "DAYE")
        {
            timeInForce = TimeInForce.Day;
            extendedHours = true;
        }

        //order type
        OrderType orderType = OrderType.Market;
        if (comboBoxMarketOrLimit.Text == "Market") orderType = OrderType.Market;
        if (comboBoxMarketOrLimit.Text == "Limit") orderType = OrderType.Limit;
        if (comboBoxMarketOrLimit.Text == "Stop") orderType = OrderType.Stop;
        if (comboBoxMarketOrLimit.Text == "Stop Limit") orderType = OrderType.StopLimit;
        if (comboBoxMarketOrLimit.Text == "Trailing Stop") orderType = OrderType.TrailingStop;

        //trailing stop
        int trialOffsetPercentage = 0;
        decimal trailOffsetDollar = 0;
        if (radioButtonTrailRate.Checked)
        {
            trialOffsetPercentage = (int)trialingRateOrPrice;
        }
        if (radioButtonTrailPrice.Checked)
        {
            trailOffsetDollar = trialingRateOrPrice;
        }

        //order qty or amount
        OrderQuantity orderQuantity = new OrderQuantity();
        if (radioButtonShares.Checked)
        {
            orderQuantity = OrderQuantity.Fractional(qty);
        }
        if (radioButtonDollars.Checked)
        {
            orderQuantity = OrderQuantity.Notional(amt);
        }

        if (Environment == "Live")
        {
            IOrder? order = await LiveBroker.SubmitOrder(orderSide, orderType, timeInForce, extendedHours, textBoxSymbol.Text, orderQuantity, stopPrice, 
                limitPrice, trialOffsetPercentage, trailOffsetDollar).ConfigureAwait(false);
            //since no onTrade event generated for Accepted status
            if (order != null && order.OrderStatus == OrderStatus.Accepted) await LiveBroker.UpdateOpenOrders().ConfigureAwait(false);
        }
        if (Environment == "Paper")
        {
            IOrder? order = await PaperBroker.SubmitOrder(orderSide, orderType, timeInForce, extendedHours, textBoxSymbol.Text, orderQuantity, stopPrice, 
                limitPrice, trialOffsetPercentage, trailOffsetDollar).ConfigureAwait(false);
            //since no onTrade event generated for Accepted status
            if (order != null && order.OrderStatus == OrderStatus.Accepted) await PaperBroker.UpdateOpenOrders().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// context Menu Strip OpenOrder Click logic
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void contextMenuStripOpenOrder_Click(object sender, EventArgs e)
    {
        try
        {
            var focusedItem = listViewOpenOrders.FocusedItem;
            var clientId = focusedItem.SubItems[6].Text;
            if (Environment == "Live")
            {
                await LiveBroker.DeleteOpenOrder(Guid.Parse(clientId));
            }
            if (Environment == "Paper")
            {
                await PaperBroker.DeleteOpenOrder(Guid.Parse(clientId));
            }
        }
        catch { }
    }

    private void radioButtonTrailRate_Click(object sender, EventArgs e)
    {
        textBoxTrailRateOrPrice.Text = "1";
    }

    private void radioButtonTrailPrice_Click(object sender, EventArgs e)
    {
        textBoxTrailRateOrPrice.Text = textBoxStopPrice.Text;
    }

    #endregion

    #region Scanner methods and events handlers

    /// <summary>
    /// Scanner Scan button cliked even
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ScanButton_Click(object? sender, EventArgs e)
    {
        Dictionary<string, IScanner>? instances = null;
        IScanner? instance = null;
        if (Environment == "Live")
        {
            instances = (Dictionary<string, IScanner>)tabControlScanners.SelectedTab.Tag;
            instance = instances["Live"];
        }
        if (Environment == "Paper")
        {
            instances = (Dictionary<string, IScanner>)tabControlScanners.SelectedTab.Tag;
            instance = instances["Paper"];
        }
        var sc = (SplitContainer?)instance?.UiContainer;
        var lv = (ListView?)sc?.Panel1.Controls[0];

        if (lv != null)
        {
            lv.BackColor = Color.LightGray;
        }
        
        if (sender != null)
        {
            var tfc = ((Button)sender).Parent;
            var tfcc = tfc.Controls;

            if (instance != null)
            {
                Type _type = instance.GetType();
                foreach (PropertyInfo p in _type.GetProperties())
                {
                    if (p.PropertyType == typeof(int) ||
                         p.PropertyType == typeof(decimal) ||
                         p.PropertyType == typeof(DateTime) ||
                         p.PropertyType == typeof(BarTimeFrameUnit) ||
                         p.PropertyType == typeof(BarTimeFrameUnit)
                    )
                    {
                        SetValuesOfControlsforScanner(instance, tfcc, _type, p);
                    }
                }
                instance.Scan();
            }
        }
    }
    /// <summary>
    /// logic to load scanned list based on environment or scanner
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Instance_ScannerListUpdated(object? sender, EventArgs e)
    {
        var instance = (IScanner?)sender;
        var sc = (SplitContainer?)instance?.UiContainer;
        var lv = (ListView?)sc?.Panel1.Controls[0];

        if (lv != null)
        {
            lv.BackColor = Color.White;

            Dictionary<string, ISnapshot> listOfsymbolAndSnapshot = ((ScannerListUpdatedEventArgs)e).ListOfsymbolAndSnapshot;
            IEnumerable<string> symbols = listOfsymbolAndSnapshot.Select(x => x.Key).ToList();

            if (instance != null)
            {
                await instance.Broker.UpdateWatchList(instance.watchList, symbols).ConfigureAwait(false);
            }

            LoadWatchListListView(lv, listOfsymbolAndSnapshot);
        }
    }

    private void TabControlScanners_Selected(object sender, TabControlEventArgs e)
    {
        LoadScannerDetails(Environment);
    }

    private void LoadScannerDetails(string env)
    {
        try
        {
            if (Environment == env)
            {
                var instances = (Dictionary<string, IScanner>)tabControlScanners.SelectedTab.Tag;
                var instance = instances[env];

                var sc = (SplitContainer)instance.UiContainer;
                var lv = (ListView)sc.Panel1.Controls[0];
                var tableLayoutPanel = (TableLayoutPanel)sc.Panel2.Controls[0];

                //update UI input criteria for scanner
                Type _type = instance.GetType();
                foreach (PropertyInfo p in _type.GetProperties())
                {
                    if ((p.PropertyType == typeof(int) ||
                         p.PropertyType == typeof(decimal) ||
                         p.PropertyType == typeof(DateTime) ||
                         p.PropertyType == typeof(BarTimeFrameUnit) ||
                         p.PropertyType == typeof(BarTimeFrameUnit)
                     ))
                    {
                        DisplayControlsForScanner(instance, tableLayoutPanel, _type, p);
                    }
                }

                //update listview of scanner
                LoadWatchListListView(lv, instance.GetScannedList());

            }
        }
        catch { }
    }
    private static void DisplayControlsForScanner(IScanner instance, TableLayoutPanel tableLayoutPanel, Type _type, PropertyInfo p)
    {
        if (p.PropertyType == typeof(decimal))
        {
            var nm = "textBox" + _type.Name + p.Name;
            TextBox tb = (TextBox)tableLayoutPanel.Controls[nm];
            tb.Text = p.GetValue(instance)?.ToString();
        }
        else if (p.PropertyType == typeof(int))
        {
            var nm = "textBox" + _type.Name + p.Name;
            TextBox tb = (TextBox)tableLayoutPanel.Controls[nm];
            tb.Text = p.GetValue(instance)?.ToString();
        }
        else if (p.PropertyType == typeof(DateTime))
        {
            var nm = "textBox" + _type.Name + p.Name;
            TextBox tb = (TextBox)tableLayoutPanel.Controls[nm];
            tb.Text = p.GetValue(instance)?.ToString();
        }
        else if (p.PropertyType == typeof(BarTimeFrameUnit))
        {
            var nm = "comboBox" + _type.Name + p.Name;
            ComboBox cb = (ComboBox)tableLayoutPanel.Controls[nm];
            cb.Text = p.GetValue(instance)?.ToString();
        }
    }
    private static void SetValuesOfControlsforScanner(IScanner instance, Control.ControlCollection tfcc, Type _type, PropertyInfo p)
    {
        if (p.PropertyType == typeof(string))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, ctl.Text);
        }
        else if (p.PropertyType == typeof(decimal))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, Convert.ToDecimal(ctl.Text));
        }
        else if (p.PropertyType == typeof(int))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, Convert.ToInt32(ctl.Text));
        }
        else if (p.PropertyType == typeof(DateTime))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, Convert.ToDateTime(ctl.Text));
        }
        else if (p.PropertyType == typeof(BarTimeFrameUnit))
        {
            var ctl = tfcc["comboBox" + _type.Name + p.Name];
            p.SetValue(instance, Enum.Parse(typeof(BarTimeFrameUnit), ctl.Text));
        }
    }
    private static void GenerateControlForScanner(Type _type, IScanner instance, TableLayoutPanel tableLayoutPanel, PropertyInfo p)
    {
        if (p.PropertyType == typeof(decimal))
        {
            TextBox tb = new()
            {
                Name = "textBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(tb);
        }
        else if (p.PropertyType == typeof(int))
        {
            TextBox tb = new()
            {
                Name = "textBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(tb);
        }
        else if (p.PropertyType == typeof(DateTime))
        {
            TextBox tb = new()
            {
                Name = "textBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(tb);
        }
        else if (p.PropertyType == typeof(BarTimeFrameUnit))
        {
            ComboBox cb = new()
            {
                DataSource = Enum.GetValues(typeof(BarTimeFrameUnit)),
                Name = "comboBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(cb);
        }
    }
    #endregion

    #region Bot methods and events handlers

    /// <summary>
    /// refresh Bot listview with current prices
    /// </summary>
    /// <param name="stock"></param>
    private void UpdateListViewBotPrices(ListView lv, IStock stock)
    {
        try
        {
            //update position list prices
            lv.Invoke(new MethodInvoker(delegate ()
            {
                if (lv.Items.Count > 0)
                {
                    ListViewItem item = lv.FindItemWithText(stock.Asset.Symbol, false, 0, false);
                    if (item != null)
                    {
                        if (stock.Last.ToString() != "")
                        {
                            if (item.SubItems[1].Text != stock.Last.ToString()) item.SubItems[1].Text = stock.Last.ToString();
                            try
                            {
                                var marketValue = (Convert.ToDecimal(item.SubItems[1].Text) * Convert.ToDecimal(item.SubItems[2].Text));
                                if (item.SubItems[3].Text != marketValue.ToString()) item.SubItems[3].Text = marketValue.ToString();
                                if (stock.OpenPositionValue != null)
                                {
                                    var profit = marketValue - Convert.ToDecimal(stock.OpenPositionValue.ToString());
                                    if (item.SubItems[4].Text != profit.ToString()) item.SubItems[4].Text = profit.ToString();
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                }
            }));
        }
        catch { }
    }

    /// <summary>
    /// load bot list with bot symbols
    /// </summary>
    /// <param name="lv"></param>
    /// <param name="listOfSymbolPosition"></param>
    private static void LoadBotListView(IBot instance, ListView lv, Dictionary<string, IPosition> listOfSymbolPosition)
    {
        try
        {
            if (listOfSymbolPosition != null)
            {
                lv.Invoke(new MethodInvoker(delegate () { lv.Items.Clear(); }));
                foreach (var assetPosition in listOfSymbolPosition.ToList())
                {
                    ListViewItem item = new(assetPosition.Key);

                    try
                    {
                        instance.ActiveSymbols.TryGetValue(assetPosition.Key, out CancellationTokenSource? cts);

                        if (cts != null)
                        {
                            item.BackColor = Color.Green;
                        }
                        if (assetPosition.Value != null)
                        {
                            item.SubItems.Add(assetPosition.Value.AssetLastPrice.ToString());
                            item.SubItems.Add(assetPosition.Value.Quantity.ToString());
                            item.SubItems.Add(assetPosition.Value.MarketValue.ToString());
                            item.SubItems.Add(assetPosition.Value.UnrealizedProfitLoss.ToString());
                            lv.Invoke(new MethodInvoker(delegate () { lv.Items.Add(item); }));
                        }
                        else
                        {
                            item.SubItems.Add("0.00");
                            item.SubItems.Add("0");
                            item.SubItems.Add("0.00");
                            item.SubItems.Add("0.00");
                            lv.Invoke(new MethodInvoker(delegate () { lv.Items.Add(item); }));
                        }
                    }
                    catch { }
                }
            }
            else
            {
                lv.Invoke(new MethodInvoker(delegate () { lv.Items.Clear(); }));
            }
        }
        catch { }
    }

    /// <summary>
    /// logic to load bot list based on environment or bot
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void contextMenuStripAddToBot_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
    {
        try
        {
            ToolStripItem tsi = e.ClickedItem;
            var symbol = ((ListViewItem.ListViewSubItem)e.ClickedItem.Owner.Tag).Text;
            if (Environment == "Live")
            {
                var instances = (Dictionary<string, IBot>)e.ClickedItem.Tag;
                var bot = instances[Environment];
                var position = await LiveBroker.GetCurrentPosition(symbol).ConfigureAwait(false);

                if (position != null)
                {
                    bot.ListOfSymbolAndPosition.Add(symbol, position);
                }
                
                bot.OnListUpdated(new BotListUpdatedEventArgs() { ListOfsymbolAndPosition = bot.ListOfSymbolAndPosition });
            }
            if (Environment == "Paper")
            {
                var instances = (Dictionary<string, IBot>)e.ClickedItem.Tag;
                var bot = instances[Environment];
                var position = await PaperBroker.GetCurrentPosition(symbol).ConfigureAwait(false);

                if (position != null)
                {
                    bot.ListOfSymbolAndPosition.Add(symbol, position);
                }
                
                bot.OnListUpdated(new BotListUpdatedEventArgs() { ListOfsymbolAndPosition = bot.ListOfSymbolAndPosition });
            }

        }
        catch { }

        LoadBotDetails(Environment);
    }

    /// <summary>
    /// handle logic when Botlist listview item is clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BotList_MouseClick(object? sender, MouseEventArgs e)
    {
        //row hit
        ListView? lv = (ListView?)sender;

        if (lv != null)
        {
            Point mousePosition = lv.PointToClient(MousePosition);
            ListViewHitTestInfo hit = lv.HitTest(mousePosition);
            int columnindex = hit.Item.SubItems.IndexOf(hit.SubItem);
            var focusedItem = lv.FocusedItem;

            if (hit.SubItem != null)
            {
                //sell or buy based position
                var symbol = focusedItem.SubItems[0].Text;

                //show context menu 
                if (e.Button == MouseButtons.Right)
                {
                    if (focusedItem != null && focusedItem.Bounds.Contains(e.Location))
                    {
                        Dictionary<string, IBot> bots = ((Dictionary<string, IBot>)tabControlBots.SelectedTab.Tag);
                        var instance = bots[Environment];
                        instance.SelectedSymbol = focusedItem.SubItems[0].Text;
                        contextMenuStripBot.Tag = instance;

                        instance.ActiveSymbols.TryGetValue(symbol, out CancellationTokenSource? cts);
                        if (cts != null)
                        {
                            contextMenuStripBot.Items[0].Enabled = false;
                            contextMenuStripBot.Items[1].Enabled = true;
                            contextMenuStripBot.Items[2].Enabled = false;
                        }
                        else
                        {
                            contextMenuStripBot.Items[0].Enabled = true;
                            contextMenuStripBot.Items[1].Enabled = true;
                            contextMenuStripBot.Items[2].Enabled = true;
                        }

                        contextMenuStripBot.Show(Cursor.Position);
                    }
                }
            }
        }
    }

    private async void Instance_BotListUpdated(object? sender, EventArgs e)
    {
        var instance = (IBot?)sender;
        var sc = (SplitContainer?)instance?.UiContainer;
        var lv = (ListView?)sc?.Panel1.Controls[0];

        if (lv != null)
        {
            lv.BackColor = Color.White;
            Dictionary<string, IPosition> listOfsymbolAndPosition = ((BotListUpdatedEventArgs)e).ListOfsymbolAndPosition;
            IEnumerable<string> symbols = listOfsymbolAndPosition.Select(x => x.Key).ToList();

            if (instance != null)
            {
                await instance.Broker.UpdateWatchList(instance.WatchList, symbols).ConfigureAwait(false);

                LoadBotListView(instance, lv, listOfsymbolAndPosition);
            }
        }
    }
    private void TabControlBots_Selected(object sender, TabControlEventArgs e)
    {
        LoadBotDetails(Environment);
    }

    private async void ToolStripMenuItemStart_Click(object sender, EventArgs e)
    {
        IBot instance = (IBot)contextMenuStripBot.Tag;
        var sc = (SplitContainer)instance.UiContainer;
        ListView botListView = (ListView)sc.Panel1.Controls[0];

        //start 
        CancellationTokenSource botTokenSource = await instance.Start(instance.SelectedSymbol);
        instance.ActiveSymbols.Add(instance.SelectedSymbol, botTokenSource);

        botListView.FocusedItem.BackColor = Color.Green;
    }

    private void toolStripMenuItemStop_Click(object sender, EventArgs e)
    {
        IBot instance = (IBot)contextMenuStripBot.Tag;
        var sc = (SplitContainer)instance.UiContainer;
        ListView botListView = (ListView)sc.Panel1.Controls[0];
        botListView.FocusedItem.BackColor = Color.White;

        //End
        instance.End(instance.ActiveSymbols[instance.SelectedSymbol]);
        instance.ActiveSymbols.Remove(instance.SelectedSymbol);
    }

    private void toolStripMenuItemDelete_Click(object sender, EventArgs e)
    {
        IBot instance = (IBot)contextMenuStripBot.Tag;
        var sc = (SplitContainer)instance.UiContainer;
        ListView botListView = (ListView)sc.Panel1.Controls[0];
        botListView.Items.Remove(botListView.FocusedItem);
        instance.ListOfSymbolAndPosition.Remove(instance.SelectedSymbol);
        if (Environment == "Paper") {
            PaperBroker.DeleteItemFromWatchList(instance.WatchList, instance.SelectedSymbol);
        }
        if (Environment == "Live")
        {
            LiveBroker.DeleteItemFromWatchList(instance.WatchList, instance.SelectedSymbol);
        }
    }

    private void LoadBotDetails(string env)
    {
        try
        {
            if (Environment == env)
            {
                var instances = (Dictionary<string, IBot>)tabControlBots.SelectedTab.Tag;
                var instance = instances[env];

                var sc = (SplitContainer)instance.UiContainer;
                var lv = (ListView)sc.Panel1.Controls[0];
                var tableLayoutPanel = (TableLayoutPanel)sc.Panel2.Controls[0];

                //update UI input criteria for scanner
                Type _type = instance.GetType();
                foreach (PropertyInfo p in _type.GetProperties())
                {
                    if ((p.PropertyType == typeof(int) ||
                         p.PropertyType == typeof(decimal) ||
                         p.PropertyType == typeof(DateTime) ||
                         p.PropertyType == typeof(BarTimeFrameUnit) ||
                         p.PropertyType == typeof(BarTimeFrameUnit)
                     ))
                    {
                        DisplayControlsForBot(instance, tableLayoutPanel, _type, p);
                    }
                }

                //update listview of bots
                LoadBotListView(instance, lv, instance.GetBotList());
            }
        }
        catch { }
    }
    private static void DisplayControlsForBot(IBot instance, TableLayoutPanel tableLayoutPanel, Type _type, PropertyInfo p)
    {
        if (p.PropertyType == typeof(decimal))
        {
            var nm = "textBox" + _type.Name + p.Name;
            TextBox tb = (TextBox)tableLayoutPanel.Controls[nm];
            tb.Text = p.GetValue(instance)?.ToString();
        }
        else if (p.PropertyType == typeof(int))
        {
            var nm = "textBox" + _type.Name + p.Name;
            TextBox tb = (TextBox)tableLayoutPanel.Controls[nm];
            tb.Text = p.GetValue(instance)?.ToString();
        }
        else if (p.PropertyType == typeof(DateTime))
        {
            var nm = "textBox" + _type.Name + p.Name;
            TextBox tb = (TextBox)tableLayoutPanel.Controls[nm];
            tb.Text = p.GetValue(instance)?.ToString();
        }
        else if (p.PropertyType == typeof(BarTimeFrameUnit))
        {
            var nm = "comboBox" + _type.Name + p.Name;
            ComboBox cb = (ComboBox)tableLayoutPanel.Controls[nm];
            cb.Text = p.GetValue(instance)?.ToString();
        }
    }
    private static void SetValuesOfControlsforBot(IBot instance, Control.ControlCollection tfcc, Type _type, PropertyInfo p)
    {
        if (p.PropertyType == typeof(string))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, ctl.Text);
        }
        else if (p.PropertyType == typeof(decimal))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, Convert.ToDecimal(ctl.Text));
        }
        else if (p.PropertyType == typeof(int))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, Convert.ToInt32(ctl.Text));
        }
        else if (p.PropertyType == typeof(DateTime))
        {
            var ctl = tfcc["textBox" + _type.Name + p.Name];
            p.SetValue(instance, Convert.ToDateTime(ctl.Text));
        }
        else if (p.PropertyType == typeof(BarTimeFrameUnit))
        {
            var ctl = tfcc["comboBox" + _type.Name + p.Name];
            p.SetValue(instance, Enum.Parse(typeof(BarTimeFrameUnit), ctl.Text));
        }
    }
    private static void GenerateControlForBot(Type _type, IBot instance, TableLayoutPanel tableLayoutPanel, PropertyInfo p)
    {
        if (p.PropertyType == typeof(decimal))
        {
            TextBox tb = new()
            {
                Name = "textBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(tb);
        }
        else if (p.PropertyType == typeof(int))
        {
            TextBox tb = new()
            {
                Name = "textBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(tb);
        }
        else if (p.PropertyType == typeof(DateTime))
        {
            TextBox tb = new()
            {
                Name = "textBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(tb);
        }
        else if (p.PropertyType == typeof(BarTimeFrameUnit))
        {
            ComboBox cb = new()
            {
                DataSource = Enum.GetValues(typeof(BarTimeFrameUnit)),
                Name = "comboBox" + _type.Name + p.Name,
                Text = p.GetValue(instance)?.ToString()
            };
            tableLayoutPanel.Controls.Add(cb);
        }
    }

    #endregion

}
