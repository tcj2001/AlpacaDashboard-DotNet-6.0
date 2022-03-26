namespace AlpacaDashboard;

public interface IStock
{
    IAsset? Asset { get; set; }
    IQuote? Quote { get; set; }
    ITrade? Trade { get; set; }
    IPosition? Position { get; set; }
    ITradeUpdate? TradeUpdate { get; set; }
    IBar? MinuteBar { get; set; }
    bool subscribed { get; set; }
    bool lastTradeOpen { get; set; }
    Guid? lastReplacedTradeId { get; set; }
    object Tag { get; set; }

}
