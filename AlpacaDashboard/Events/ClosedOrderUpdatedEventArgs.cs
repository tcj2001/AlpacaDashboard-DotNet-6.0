namespace AlpacaDashboard.Brokers;
#region Event Arg classes
public class ClosedOrderUpdatedEventArgs : EventArgs
{
    public IReadOnlyCollection<IOrder>? ClosedOrders { get; set; }
}

#endregion

