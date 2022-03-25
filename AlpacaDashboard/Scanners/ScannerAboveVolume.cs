namespace AlpacaDashboard.Scanners;

internal class ScannerAboveVolume : IScanner
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
    private decimal _minVolume = 1000000;
    public decimal MinVolume { get => _minVolume; set => _minVolume = value; }
    #endregion


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
        //get a list of asset
        var assets = await Broker.GetAssets(AssetClass.UsEquity);

        //get a list of crypto  asset
        var selectedAssets = assets.Where(x => x.IsTradable).ToList();

        //get a list of snapshots for the selected symbols
        var assetAndSnapshots = await Broker.ListSnapShots(selectedAssets, 5000);

        // logic for selecting symbols with MinClose, MaxClose and MinVolume
        Dictionary<IAsset, ISnapshot?> selectedAssetAndSnapShot = new();
        foreach (var item in assetAndSnapshots)
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
                    selectedAssetAndSnapShot.Add(item.Key, item.Value);
                }
            }
        }

        //subscribe all selected symbols
        IEnumerable<IAsset> assets2 = selectedAssetAndSnapShot.Select(x => x.Key);
        await Broker.Subscribe(assets2, 5000, "Scanner").ConfigureAwait(false); 

        //symbol and snapshot list as passed by the generated event to load listview
        ListOfAssetAndSnapshot = selectedAssetAndSnapShot;
        ScannerListUpdatedEventArgs opuea = new ScannerListUpdatedEventArgs
        {
            ListOfAssetAndSnapshot = ListOfAssetAndSnapshot
        };
        OnListUpdated(opuea);

    }


}

