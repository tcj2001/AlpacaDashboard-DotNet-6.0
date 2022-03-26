namespace AlpacaDashboard.Brokers;
#region Event Arg classes
public class OpenOrderUpdatedEventArgs : EventArgs
{
    public IReadOnlyCollection<IOrder>? OpenOrders { get; set; }
}

#endregion

