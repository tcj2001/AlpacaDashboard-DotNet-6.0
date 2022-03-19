using Alpaca.Markets;
using AlpacaDashboard.Brokers;

namespace AlpacaDashboard.Scanners
{
    internal class ScannerAboveVolume : IScanner
    {

        #region Required
        //define public properites that need to dynamically generate input controls, Broker will be ignored

        //Broker Environment
        public Broker Broker { get; set; }

        //WatchList
        public IWatchList watchList { get; set; }

        //UI screen container
        public Control UiContainer { get; set; }

        //event hander to indicate scan finished
        public event EventHandler<ScannerListUpdatedEventArgs> ScannerListUpdated;


        //list to hold symbol and last bar of the time frame
        public Dictionary<string, ISnapshot> ListOfSymbolAndSnapshot { get; set; }

        //TimeFrame unit
        private BarTimeFrameUnit _BarTimeFrameUnit = BarTimeFrameUnit.Day;
        public BarTimeFrameUnit BarTimeFrameUnit { get => _BarTimeFrameUnit; set => _BarTimeFrameUnit = value; }

        //Required BarTimeFrameUnit 
        private int _BarTimeFrameCount = 30;
        public int BarTimeFrameCount { get => _BarTimeFrameCount; set => _BarTimeFrameCount = value; }

        /// <summary>
        /// Get a list of scanned symbols
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, ISnapshot> GetScannedList()
        {
            return ListOfSymbolAndSnapshot;
        }

        /// <summary>
        /// event generated for UI when scanner is updated
        /// </summary>
        /// <param name="e"></param>
        public void OnListUpdated(ScannerListUpdatedEventArgs e)
        {
            EventHandler<ScannerListUpdatedEventArgs> handler = ScannerListUpdated;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        #endregion

        //Define all other field that need to shown on the UI
        //Minimum close
        private decimal _minClose = 3;
        public decimal MinClose { get => _minClose; set => _minClose = value; }

        //Maximum close
        private decimal _maxClose = 5000;
        public decimal MaxClose { get => _maxClose; set => _maxClose = value; }

        //Minimum volume
        private decimal _minVolume = 1000000;
        public decimal MinVolume { get => _minVolume; set => _minVolume = value; }

        //Define all other field that need to shown on the UI
        //none


        public ScannerAboveVolume(Broker broker)
        {
            this.Broker = broker;
        }


        /// <summary>
        /// Loigic to select scaaner symbols
        /// </summary>
        /// <returns></returns>
        public async Task Scan()
        {
            CancellationToken token = new CancellationToken();

            //get a list of asset
            var assets = await Broker.GetAssets(AssetClass.UsEquity);

            //get a list of crypto  asset
            var selectedAssets = assets.Where(x => x.IsTradable).ToList();

            //get a list of snapshots for the selected symbols
            var symbolAndSnapshots = await Broker.ListSnapShots(selectedAssets, 5000);

            // logic for selecting symbols with MinClose, MaxClose and MinVolume
            Dictionary<string, ISnapshot> selectedSnapShotSymbols = new();
            foreach (var item in symbolAndSnapshots)
            {
                bool select = true;
                if (item.Value.CurrentDailyBar != null)
                {
                    if (!(item.Value.CurrentDailyBar.Close >= MinClose && item.Value.CurrentDailyBar.Close <= MaxClose))
                        select = false;
                    if (!(item.Value.CurrentDailyBar.Volume >= MinVolume))
                        select = false;
                    if (select)
                    {
                        selectedSnapShotSymbols.Add(item.Key, item.Value);
                    }
                }
            }

            //subscribe all selected symbols
            IEnumerable<string> symbols = selectedSnapShotSymbols.Select(x => x.Key);
            await Stock.Subscribe(Broker, symbols, "Scanner");

            //symbol and snapshot list as passed by the generated event to load listview
            ListOfSymbolAndSnapshot = selectedSnapShotSymbols;
            ScannerListUpdatedEventArgs opuea = new ScannerListUpdatedEventArgs
            {
                ListOfsymbolAndSnapshot = ListOfSymbolAndSnapshot
            };
            OnListUpdated(opuea);

        }


    }

}
