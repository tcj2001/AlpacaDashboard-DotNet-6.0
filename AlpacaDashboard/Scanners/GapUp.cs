global using OoplesFinance.StockIndicators.Models;
global using static OoplesFinance.StockIndicators.Calculations;

namespace AlpacaDashboard.Scanners;

internal class GapUp : IScanner
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
    //Minimum close
    private decimal _minClose = 3;
    public decimal MinClose { get => _minClose; set => _minClose = value; }

    //Maximum close
    private decimal _maxClose = 5000;
    public decimal MaxClose { get => _maxClose; set => _maxClose = value; }

    //Minimum volume
    private decimal _minVolume = 1000;
    public decimal MinVolume { get => _minVolume; set => _minVolume = value; }

    //Gap Up Percentage
    private int _gapUpPerc = 7;
    public int GapUpPerc { get => _gapUpPerc; set => _gapUpPerc = value; }

    //Refresh Scanner interval
    private int _refreshInterval = 3;
    public int RefreshInterval { get => _refreshInterval; set => _refreshInterval = value; }
    #endregion

    public GapUp(Broker broker) => Broker = broker;

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
                if (item.Value?.CurrentDailyBar != null && item.Value?.PreviousDailyBar != null)
                {

                    if (!(item.Value?.CurrentDailyBar.Close >= MinClose && item.Value.CurrentDailyBar.Close <= MaxClose))
                        select = false;
                    if (!(item.Value?.CurrentDailyBar.Volume >= MinVolume))
                        select = false;
                    if (!(item.Value?.CurrentDailyBar.Close < item.Value?.PreviousDailyBar.Close * 1 + _gapUpPerc/ 100))
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
        await Broker.Subscribe(assetLists, 5000, "Scanner").ConfigureAwait(false);

        //get snapshot again for the above selected assets
        var symbolAndSnapshots2 = await Broker.ListSnapShots(assetLists, 5000).ConfigureAwait(false);

        //symbol and snapshot list as passed by the generated event to load listview
        ListOfAssetAndSnapshot = symbolAndSnapshots2;
        ScannerListUpdatedEventArgs opuea = new()
        {
            ListOfAssetAndSnapshot = ListOfAssetAndSnapshot
        };
        OnListUpdated(opuea);
    }
}
