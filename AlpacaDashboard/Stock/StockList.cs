global using System.Collections;

namespace AlpacaDashboard;

public class StockList : IEnumerable<IStock>
{
    public IList<IStock> stocks = new List<IStock>();

    public StockList()
    {
    }
    public StockList(IStock stock)
    {
        stocks.Add(stock);
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
            return stocks.ToList().Where(x => x.Asset?.Symbol == symbol).FirstOrDefault();
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
        var stock = stocks.ToList().Where(x => x.Asset?.Symbol == asset.Symbol).FirstOrDefault();
        return stock;
    }

    /// <summary>
    /// Get Stock List
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks()
    {
        return stocks.ToList();
    }

    /// <summary>
    /// Get Stock List for symbol list
    /// </summary>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks(IEnumerable<string> symbols)
    {
        return stocks.ToList().Where(a => symbols.Any(s => s == a.Asset?.Symbol));
    }

    /// <summary>
    /// Get a list of Stock for a Asset Class
    /// </summary>
    /// <param name="assetClass"></param>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks(AssetClass assetClass)
    {
        var stockList = stocks.ToList().Where(x => x.Asset?.Class == assetClass);
        return stockList;
    }

    /// <summary>
    /// Get stock list for a asset class and list of symbols
    /// </summary>
    /// <param name="assetClass"></param>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IStock> GetStocks(AssetClass assetClass, IEnumerable<string> symbols)
    {
        var stockList = stocks.ToList().Where(x => x.Asset?.Class == assetClass && symbols.Any(s => s == x.Asset.Symbol));
        return stockList;
    }
    #endregion

    #region Get Asset methods
    /// <summary>
    /// Get a list of Asset from Stocks 
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets()
    {
        var assetList = stocks.ToList().Select(x => x.Asset).ToList();
        return assetList;
    }

    /// <summary>
    /// Get Asset List for symbol list
    /// </summary>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets(IEnumerable<string> symbols)
    {
        return stocks.ToList().Where(a => symbols.Any(s => s == a.Asset?.Symbol)).Select(x => x.Asset);
    }

    /// <summary>
    /// Get a list of Asset from Stocks for a Asset Class
    /// </summary>
    /// <param name="assetClass"></param>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets(AssetClass assetClass)
    {
        var assetList = stocks.ToList().Where(x => x.Asset?.Class == assetClass).Select(x => x.Asset);
        return assetList;
    }

    /// <summary>
    /// Get a list of Asset from Stocks for a Asset Class
    /// </summary>
    /// <param name="assetClass"></param>
    /// <param name="symbols"></param>
    /// <returns></returns>
    public IEnumerable<IAsset?> GetAssets(AssetClass assetClass, IEnumerable<string> symbols)
    {
        var assetList = stocks.ToList().Where(x => x.Asset?.Class == assetClass && symbols.Any(s => s == x.Asset.Symbol)).Select(x => x.Asset).ToList();
        return assetList;
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
        var symbolList = stocks.ToList().Where(x => x.Asset?.Class == assetClass).Select(x => x.Asset?.Symbol);
        return symbolList;
    }
    #endregion

    /// <summary>
    /// Add new Stock
    /// </summary>
    /// <param name="stock"></param>
    public void Add(Stock stock)
    {
        stocks.Add(stock);
    }

    #region enumerator methods
    /// <summary>
    /// Get a List of Stock
    /// </summary>
    /// <returns></returns>
    public IEnumerator<IStock> GetEnumerator()
    {
        foreach (Stock stock in stocks)
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
