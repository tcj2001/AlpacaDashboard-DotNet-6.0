namespace AlpacaDashboard.Scanners;

internal class ScannerCrypto : IScanner
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

    #region properites that will be shown on UI
    #endregion

    public ScannerCrypto(Broker broker)
    {
        this.Broker = broker;
    }


    /// <summary>
    /// Loigic to select scaaner symbols
    /// </summary>
    /// <returns></returns>
    public async Task Scan()
    {
        //get a list of asset
        var assets = await Broker.GetAssets(AssetClass.Crypto).ConfigureAwait(false);

        //get a list of crypto  asset
        var selectedAssets = assets.Where(x => x.IsTradable).ToList();

        //get a list of snapshots for the selected symbols
        var assetAndSnapshots = await Broker.ListSnapShots(selectedAssets, 5000).ConfigureAwait(false);

        //subscribe all selected symbols
        IEnumerable<IAsset> assets2 = assetAndSnapshots.Select(x => x.Key).ToList();
        await Broker.Subscribe(assets2, 5000, "Scanner").ConfigureAwait(false);

        //symbol and snapshot list as passed by the generated event to load listview
        ListOfAssetAndSnapshot = assetAndSnapshots;
        ScannerListUpdatedEventArgs opuea = new()
        {
            ListOfAssetAndSnapshot = ListOfAssetAndSnapshot
        };
        OnListUpdated(opuea);

    }

}

