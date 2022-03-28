namespace AlpacaDashboard;

public class PaperKey
{
    public string API_KEY { get; set; } = default!;
    public string API_SECRET { get; set; } = default!;
}

public class LiveKey
{
    public string API_KEY { get; set; } = default!;
    public string API_SECRET { get; set; } = default!;
}

public class MySettings
{
    public bool Subscribed { get; set; }
    public int PriceUpdateInterval { get; set; }
    public string CryptoExchange { get; set; } = default!;
}
