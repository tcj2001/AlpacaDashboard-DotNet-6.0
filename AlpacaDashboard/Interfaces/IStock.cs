namespace AlpacaDashboard;

public interface IStock
{
    IAsset? Asset { get; set; }
    IQuote? Quote { get; set; }
    ITrade? Trade { get; set; }
    IPosition? Position { get; set; }
    //hold the order id of the order that was replaced by this order
    List<IOrder> OpenOrders { get; set; } 
    ITradeUpdate? TradeUpdate { get; set; }
    IBar? MinuteBar { get; set; }
    bool subscribed { get; set; }
    object Tag { get; set; }
}
