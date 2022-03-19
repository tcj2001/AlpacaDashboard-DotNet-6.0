﻿namespace AlpacaDashboard.Scanners;

internal class ScannerCrypto : IScanner
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
    //none


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
        var symbolAndSnapshots = await Broker.ListSnapShots(selectedAssets, 5000).ConfigureAwait(false);

        //subscribe all selected symbols
        IEnumerable<string> symbols = symbolAndSnapshots.Select(x => x.Key).ToList();
        await Stock.Subscribe(Broker, symbols, "Scanner").ConfigureAwait(false);

        //symbol and snapshot list as passed by the generated event to load listview
        ListOfSymbolAndSnapshot = symbolAndSnapshots;
        ScannerListUpdatedEventArgs opuea = new()
        {
            ListOfsymbolAndSnapshot = ListOfSymbolAndSnapshot
        };
        OnListUpdated(opuea);

    }

}

