﻿global using OoplesFinance.StockIndicators.Models;
global using static OoplesFinance.StockIndicators.Calculations;

namespace AlpacaDashboard.Scanners;

internal class Momentum : IScanner
{
    #region Required
    //Broker Environment
    public Broker Broker { get; set; } = default!;

    //WatchList
    public IWatchList watchList { get; set; } = default!;

    //UI screen container
    public Control UiContainer { get; set; } = new();

    //event hander to indicate scan finished
    public event EventHandler<ScannerListUpdatedEventArgs> ScannerListUpdated = default!;

    //list to hold symbol and last bar of the time frame
    public Dictionary<IAsset, ISnapshot?> ListOfAssetAndSnapshot { get; set; } = new();

    /// <summary>
    /// Get a list of scanned symbols
    /// </summary>
    /// <returns></returns>
    public Dictionary<IAsset, ISnapshot?> GetScannedList()
    {
        return ListOfAssetAndSnapshot;
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

    #region properites that will be shown on UI
    //TimeFrame unit
    private BarTimeFrameUnit _BarTimeFrameUnit = BarTimeFrameUnit.Day;
    public BarTimeFrameUnit BarTimeFrameUnit { get => _BarTimeFrameUnit; set => _BarTimeFrameUnit = value; }

    //Required BarTimeFrameUnit 
    private int _BarTimeFrameCount = 1;
    public int BarTimeFrameCount { get => _BarTimeFrameCount; set => _BarTimeFrameCount = value; }

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
    private int _SmaLength = 20;
    public int SmaLength { get => _SmaLength; set => _SmaLength = value; }

    //Top N symbols
    private int _TopNSymbols = 10;
    public int TopNSymbols { get => _TopNSymbols; set => _TopNSymbols = value; }
    
    //Refresh Scanner interval
    private int _refreshInterval = 5;
    public int RefreshInterval { get => _refreshInterval; set => _refreshInterval = value; }
    #endregion

    public Momentum(Broker broker) => Broker = broker;

    /// <summary>
    /// Loigic to select scaaner symbols
    /// </summary>
    /// <returns></returns>
    public async Task Scan()
    {
        var token = new CancellationToken();
        await Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Scanner().ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMinutes(RefreshInterval), token).ConfigureAwait(false);
            }
        }, token);
    }

    private async Task Scanner()
    {
        List<KeyValuePair<string, IBar>> ListOfSymbolAndLastBar = new();

        //get a list of asset
        var assets = await Broker.GetAssets(AssetClass.UsEquity);

        //get a list of tradable asset
        var selectedAssets = assets.Where(x => x.IsTradable).ToList();

        //get a list of snapshots for the selected symbols
        var AssetlAndSnapshots = await Broker.ListSnapShots(selectedAssets, 5000);

        // logic for selecting symbols with MinClose, MaxClose and MinVolume
        Dictionary<IAsset, ISnapshot?> selectedAssetandSnapShot = new();

        foreach (var item in AssetlAndSnapshots)
        {
            try
            {
                bool select = true;
                if (item.Value?.CurrentDailyBar != null)
                {

                    if (!(item.Value?.CurrentDailyBar.Close >= MinClose && item.Value.CurrentDailyBar.Close <= MaxClose))
                        select = false;
                    if (!(item.Value?.CurrentDailyBar.Volume >= MinVolume))
                        select = false;
                    if (select)
                    {
                        selectedAssetandSnapShot.Add(item.Key, item.Value);
                    }
                }
            }
            catch { }
        }

        var timeUtc = DateTime.UtcNow;
        TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);

        //for those selected symbol's Assets
        IEnumerable<IAsset> assetLists = selectedAssetandSnapShot.Select(x => x.Key).ToList();
        //get a list of stock with its historical bars, process all symbols but in chuck of 5000 symbols at a time
        var ListOfSlowAssetAndItsBars = await Broker.ListHistoricalBars(assetLists, new BarTimeFrame(BarTimeFrameCount, BarTimeFrameUnit), SmaLength, 5000, easternTime);

        //list to hold selected assets
        Dictionary<IAsset,decimal> assetDict = new();
        foreach (var bars in ListOfSlowAssetAndItsBars)
        {
            //add logic to use ooplesFinance package and its indicator to filter symbols fitting the indicator criteria
            var stockData = new StockData(
            bars.Value.Select(x => x.Open), bars.Value.Select(x => x.High),
                bars.Value.Select(x => x.Low), bars.Value.Select(x => x.Close),
                bars.Value.Select(x => x.Volume), bars.Value.Select(x => x.TimeUtc)
            );
            var result = new List<decimal>(stockData.CalculateSimpleMovingAverage(SmaLength).CustomValuesList);
            if (result.Count() >= 2) {
                var percChange = CalculateChange(result.SkipLast(1).Last(), result.Last());
                assetDict.Add(bars.Key, percChange);
            }
        }
        var sortedDictofNItem = assetDict.OrderByDescending(x => x.Value).Take(TopNSymbols);
        var assetLists2 = sortedDictofNItem.Select(x => x.Key);

        //subscribe all selected symbols
        await Broker.Subscribe(assetLists2, 5000, "Scanner").ConfigureAwait(false);

        //get snapshot again for the above selected assets
        var AssetAndSnapshotsx = await Broker.ListSnapShots(assetLists2, 5000).ConfigureAwait(false);
        //sort it back based on percentage change of assetLists2
        Dictionary<IAsset, ISnapshot?> AssetAndSnapshots2 = new();
        assetLists2.ToList().ForEach(s =>
        {
            if (AssetAndSnapshotsx.ContainsKey(s))
                AssetAndSnapshots2.Add(s, AssetAndSnapshotsx[s]);
        });

        //symbol and snapshot list as passed by the generated event to load listview
        ScannerListUpdatedEventArgs opuea = new()
        {
            ListOfAssetAndSnapshot = AssetAndSnapshots2
        };
        OnListUpdated(opuea);
    }

    decimal CalculateChange(decimal previous, decimal current)
    {
        if (previous == 0)
            throw new InvalidOperationException();

        var change = current - previous;
        return change / previous;
    }
}
