namespace AlpacaDashboard.Brokers;
#region Event Arg classes

public class PositionUpdatedEventArgs : EventArgs
{
    public IReadOnlyCollection<IPosition>? Positions { get; set; }
}

#endregion

