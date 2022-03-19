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
    Dictionary<string, ISnapshot> ListOfSymbolAndSnapshot { get; set; }

    //TimeFrame unit
    BarTimeFrameUnit BarTimeFrameUnit { get; set; }

    //TimeFrame count
    int BarTimeFrameCount { get; set; }

    //start scan
    Task Scan();

    //Return list of symbol and Last Bar
    Dictionary<string, ISnapshot> GetScannedList();

    //scanner list updated event
    void OnListUpdated(ScannerListUpdatedEventArgs e);

}
