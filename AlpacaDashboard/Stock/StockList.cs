global using System.Collections;
global using System.Collections.Concurrent;

namespace AlpacaDashboard;

public class StockList : IEnumerable<IStock>
{
    public ConcurrentQueue<IStock> Stocks { get; set; } = new();

    public StockList()
    {
    }

    public StockList(IStock stock)
    {
        Stocks.Enqueue(stock);
    }

    #region Get Stock methods
    /// <summary>
    /// Get a Stock Object for a symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public IStock? GetStock(string? symbol)
    {
        try
        {
            return Stocks.Where(x => x.Asset?.Symbol == symbol).FirstOrDefault();
        }
        catch { return null; }
    }


    /// <summary>
    /// Get stock for a Asset
    /// </summary>
    /// <param name="asset"></param>
    /// <returns></returns>
    public IStock? GetStock(IAsset asset)
    {
        return Stocks.Where(x => x.Asset?.Symbol == asset.Symbol).FirstOrDefault();
    }

    /// <summary>
    /// Get Stock List
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks()
    {
        return Stocks;
    }

    /// <summary>
    /// Get Stock List for symbol list
    /// </summary>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks(IEnumerable<string> symbols)
    {
        return Stocks.Where(a => symbols.Any(s => s == a.Asset?.Symbol));
    }

    /// <summary>
    /// Get a list of Stock for a Asset Class
    /// </summary>
    /// <param name="assetClass"></param>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks(AssetClass assetClass)
    {
        return Stocks.Where(x => x.Asset?.Class == assetClass);
    }

    /// <summary>
    /// Get stock list for a asset class and list of symbols
    /// </summary>
    /// <param name="assetClass"></param>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks(AssetClass assetClass, IEnumerable<string> symbols)
    {
        return Stocks.Where(x => x.Asset?.Class == assetClass && symbols.Any(s => s == x.Asset.Symbol));
    }
    #endregion

    #region Get Asset methods
    /// <summary>
    /// Get a list of Asset from Stocks 
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets()
    {
        return Stocks.Select(x => x.Asset);
    }

    /// <summary>
    /// Get Asset List for symbol list
    /// </summary>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets(IEnumerable<string> symbols)
    {
        return Stocks.Where(a => symbols.Any(s => s == a.Asset?.Symbol)).Select(x => x.Asset);
    }

    /// <summary>
    /// Get a list of Asset from Stocks for a Asset Class
    /// </summary>
    /// <param name="assetClass"></param>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets(AssetClass assetClass)
    {
        return Stocks.Where(x => x.Asset?.Class == assetClass).Select(x => x.Asset);
    }

    /// <summary>
    /// Get a list of Asset from Stocks for a Asset Class
    /// </summary>
    /// <param name="assetClass"></param>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets(AssetClass assetClass, IEnumerable<string> symbols)
    {
        return Stocks.Where(x => x.Asset?.Class == assetClass && symbols.Any(s => s == x.Asset.Symbol)).Select(x => x.Asset);
    }
    #endregion

    #region Get Symbol methods
    /// <summary>
    /// Get a list of symbols for a Asset Class
    /// </summary>
    /// <param name="assetClass"></param>
    /// <returns></returns>
    public IEnumerable<string?> GetSymbols(AssetClass assetClass)
    {
        var symbolList = Stocks.ToList().Where(x => x.Asset?.Class == assetClass).Select(x => x.Asset?.Symbol);
        return symbolList;
    }
    #endregion

    /// <summary>
    /// Add new Stock
    /// </summary>
    /// <param name="stock"></param>
    public IStock Add(Stock stock)
    {
        Stocks.Enqueue(stock);
        return stock;
    }

    #region enumerator methods
    /// <summary>
    /// Get a List of Stock
    /// </summary>
    /// <returns></returns>
    public IEnumerator<IStock> GetEnumerator()
    {
        foreach (Stock stock in Stocks)
        {
            yield return stock;
        }
    }

    /// <summary>
    /// Enumerable
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    #endregion

}
