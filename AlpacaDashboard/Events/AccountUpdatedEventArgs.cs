namespace AlpacaDashboard.Brokers;
#region Event Arg classes
public class AccountUpdatedEventArgs : EventArgs
{
    public IAccount? Account { get; set; }
}

#endregion

