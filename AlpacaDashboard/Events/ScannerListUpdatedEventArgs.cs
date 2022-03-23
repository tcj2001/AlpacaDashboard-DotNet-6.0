namespace AlpacaDashboard;

/// <summary>
/// This Event arg class is used by all scanners
/// </summary>
public class ScannerListUpdatedEventArgs : EventArgs
{
    public Dictionary<IAsset, ISnapshot?> ListOfAssetAndSnapshot { get; set; } = new();
}
