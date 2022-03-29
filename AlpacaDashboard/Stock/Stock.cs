global using Alpaca.Markets;
global using Alpaca.Markets.Extensions;

namespace AlpacaDashboard;

public class Stock : IStock
{
    public object Tag { get; set; } = default!;
    public IAsset? Asset { get; set; } = default!;
    public IQuote? Quote { get; set; } = default!;
    public ITrade? Trade { get; set; } = default!;
    public IPosition? Position { get; set; } = default!;
    //hold the order id of the order that was replaced by this order
    public List<Guid> OpenOrders { get; set; } = new List<Guid>();
    public ITradeUpdate? TradeUpdate { get; set; } = default!;
    public IBar? MinuteBar { get; set; } = default!;
    public bool subscribed { get; set; } = default!;

    /// <summary>
    /// instantiate stock object
    /// </summary>
    /// <param name="alpacaCryptoStreamingClient"></param>
    /// <param name="alpacaDataStreamingClient"></param>
    /// <param name="asset"></param>
    /// <param name="type"></param>
    public Stock(IAsset asset, string type)
    {
        this.Asset = asset;
        this.Tag = (object)type;
    }
}
