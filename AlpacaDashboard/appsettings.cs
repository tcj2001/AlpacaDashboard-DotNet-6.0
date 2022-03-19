namespace AlpacaDashboard
{
    public class PaperKey
    {
        public string API_KEY { get; set; }
        public string API_SECRET { get; set; }

    }

    public class LiveKey
    {
        public string API_KEY { get; set; }
        public string API_SECRET { get; set; }

    }

    public class MySettings
    {
        public bool Subscribed { get; set; }
        public int UnScibscribedRefreshInterval { get; set; }
        public int PriceUpdateInterval { get; set; }
        public string CryptoExchange { get; set; }
    }
}
