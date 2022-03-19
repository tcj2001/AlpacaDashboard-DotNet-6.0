using Alpaca.Markets;

namespace AlpacaDashboard
{
    /// <summary>
    /// This Event arg class is used by all scanners
    /// </summary>
    public class ScannerListUpdatedEventArgs : EventArgs
    {
        public Dictionary<string, ISnapshot> ListOfsymbolAndSnapshot { get; set; }
    }
}
