global using OoplesFinance.StockIndicators.Models;
global using static OoplesFinance.StockIndicators.Calculations;

namespace AlpacaDashboard.Scanners;

internal class ScannerAboveSMA : IScanner
{
    #region Required
    //define public properites that need to dynamically generate input controls, Broker will be ignored

    //Broker Environment
    public Broker Broker { get; set; } = default!;

    //WatchList
    public IWatchList watchList { get; set; } = default!;

    //UI screen container
    public Control UiContainer { get; set; } = new();

    //event hander to indicate scan finished
    public event EventHandler<ScannerListUpdatedEventArgs> ScannerListUpdated = default!;

    //list to hold symbol and last bar of the time frame
    public Dictionary<string, ISnapshot> ListOfSymbolAndSnapshot { get; set; } = new();

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
    /// event generated for UI when list is updated
    /// </summary>
    /// <param name="e"></param>
    public void OnListUpdated(ScannerListUpdatedEventArgs e)
    {
        ScannerListUpdated?.Invoke(this, e);
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

    //SMA length
    private int _SmaLength = 14;
    public int SmaLength { get => _SmaLength; set => _SmaLength = value; }

    //Define all other field that need to shown on the UI
    //none

    public ScannerAboveSMA(Broker broker) => Broker = broker;

    /// <summary>
    /// Loigic to select scaaner symbols
    /// </summary>
    /// <returns></returns>
    public async Task Scan()
    {
        List<KeyValuePair<string, IBar>> ListOfSymbolAndLastBar = new();

        //get a list of asset
        var assets = await Broker.GetAssets(AssetClass.UsEquity);

        //get a list of tradable asset
        var selectedAssets = assets.Where(x => x.IsTradable).ToList();

        //get a list of snapshots for the selected symbols
        var symbolAndSnapshots = await Broker.ListSnapShots(selectedAssets, 5000);

        // logic for selecting symbols with MinClose, MaxClose and MinVolume
        Dictionary<string, ISnapshot> selectedSnapShotSymbols = new();

        foreach (var item in symbolAndSnapshots)
        {
            try
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
            catch { }
        }

        var timeUtc = DateTime.UtcNow;
        TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);

        //for those selected symbol's Assets
        IEnumerable<IAsset> assetLists = assets.Where(a => selectedSnapShotSymbols.Any(s => s.Key == a.Symbol));
        //get a list of stock with its historical bars, process all symbols but in chuck of 5000 symbols at a time
        var ListOfSymbolAndItsBars = await Broker.ListHistoricalBars(assetLists, BarTimeFrameUnit, BarTimeFrameCount, 5000, easternTime);

        //list to hold selected symbols
        List<string> symbols = new();
        foreach (var bars in ListOfSymbolAndItsBars)
        {
            //add logic to use ooplesFinance package and its indicator to filter symbols fitting the indicator criteria
            var stockData = new StockData(
            bars.Value.Select(x => x.Open), bars.Value.Select(x => x.High),
                bars.Value.Select(x => x.Low), bars.Value.Select(x => x.Close),
                bars.Value.Select(x => x.Volume), bars.Value.Select(x => x.TimeUtc)
            );
            var result = stockData.CalculateSimpleMovingAverage(SmaLength);

            //if last close price > last sma price , i.e above sma
            if (result.ClosePrices.Last() > result.CustomValuesList.Last())
            {
                symbols.Add(bars.Key);
            }
        }

        //subscribe all selected symbols
        await Stock.Subscribe(Broker, symbols, "Scanner");

        //for those selected symbol get asset list
        IEnumerable<IAsset> assetLists2 = assets.Where(a => symbols.Contains(a.Symbol));
        //get snapshot again for the above selected assets
        var symbolAndSnapshots2 = await Broker.ListSnapShots(assetLists2, 5000);

        //symbol and snapshot list as passed by the generated event to load listview
        ListOfSymbolAndSnapshot = symbolAndSnapshots2;
        ScannerListUpdatedEventArgs opuea = new()
        {
            ListOfsymbolAndSnapshot = ListOfSymbolAndSnapshot
        };
        OnListUpdated(opuea);
    }
}
