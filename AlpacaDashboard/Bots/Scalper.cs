using Alpaca.Markets;
using AlpacaDashboard.Brokers;

namespace AlpacaDashboard
{
    internal class Scalper : IBot
    {
        #region Required
        //Broker Environment
        public Broker Broker { get; set; }

        //WatchList
        public IWatchList WatchList { get; set; }

        //selected symbol
        public string SelectedSymbol { get; set; }

        //Active symbols
        public Dictionary<string, CancellationTokenSource> ActiveSymbols { get; set; }

        //UI screen container
        public Control UiContainer { get; set; }

        //event hander to indicate scan finished
        public event EventHandler<BotListUpdatedEventArgs> BotListUpdated;

        //list to hold symbol and last bar of the time frame
        public Dictionary<string, IPosition> ListOfSymbolAndPosition { get; set; }

        //TimeFrame unit
        private BarTimeFrameUnit _BarTimeFrameUnit = BarTimeFrameUnit.Minute;
        public BarTimeFrameUnit BarTimeFrameUnit { get => _BarTimeFrameUnit; set => _BarTimeFrameUnit = value; }

        //Required BarTimeFrameUnit 
        private int _BarTimeFrameCount = 1;
        public int BarTimeFrameCount { get => _BarTimeFrameCount; set => _BarTimeFrameCount = value; }


        //Define all other field that need to be shown on the UI
        //none


        /// <summary>
        /// Get a list of bot symbols
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, IPosition> GetBotList()
        {
            return ListOfSymbolAndPosition;
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

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="broker"></param>
        public Scalper(Broker broker)
        {
            this.Broker = broker;
            ActiveSymbols = new Dictionary<string, CancellationTokenSource>();
        }


        /// <summary>
        /// Start Method
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<CancellationTokenSource> Start(string symbol)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            //get stock object of the symbol
            IStock stock = null;
            if (Broker.Environment == "Paper")
            {
                stock = Stock.PaperStockObjects.GetStock(symbol);
            }
            if (Broker.Environment == "Live")
            {
                stock = Stock.LiveStockObjects.GetStock(symbol);
            }

            //Run you bot logic until cancelled
            Task task = Task.Run(() => BotLogic(stock, BarTimeFrameUnit, BarTimeFrameCount, source.Token), source.Token);

            await task;

            return source;
        }
    
        /// <summary>
        /// End Method
        /// </summary>
        /// <param name="source"></param>
        public void End(CancellationTokenSource source)
        {
            source.Cancel();
        }

        /// <summary>
        /// Main Bot Logic is in here
        /// </summary>
        /// <param name="stock"></param>
        /// <param name="barTimeFrameUnit"></param>
        /// <param name="barTimeFrameCount"></param>
        /// <param name="token"></param>

        private async void BotLogic(IStock stock, BarTimeFrameUnit barTimeFrameUnit, int barTimeFrameCount, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }

    }
}
