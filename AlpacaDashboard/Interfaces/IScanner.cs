namespace AlpacaDashboard;

public interface IScanner
{
    //Broker Environment
    Broker Broker { get; set; }

    //WatchList
    IWatchList watchList { get; set; }

    //UI screen container
    Control UiContainer { get; set; }

    //event hander to indicate scan finished
    public event EventHandler<ScannerListUpdatedEventArgs> ScannerListUpdated;

    //list to hold symbol and last bar of the time frame
    Dictionary<IAsset, ISnapshot?> ListOfAssetAndSnapshot { get; set; }

    //start scan
    Task Scan();

    //Return list of symbol and Last Bar
    Dictionary<IAsset, ISnapshot?> GetScannedList();

    //scanner list updated event
    void OnListUpdated(ScannerListUpdatedEventArgs e);

}
