namespace AlpacaDashboard;

public class StockUpdatedEventArgs : EventArgs
{
    public IEnumerable<IStock>? Stocks { get; set; }
}
