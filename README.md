# Alpaca Dashboard (.Net 6.0)

This is Alpaca Dashboard which will allow you to manage you portfolio in Live and Paper Environment. It will list your open position, open orders, closed orders and watchlist of open positions, these will show real time updates on the close price, position, market value, profit/loss, bid price and ask price for each position.
Account data like buying power will updated realtime. You will also have the ability to submit new order or close position etc.  

You can build Scanner and Bots classes easily by implementing its interfaces. each implemented scanner will be automaticlly displayed in UI along with its imput parameters.  

Portfolio screen  
![Imgur](https://i.imgur.com/y3wP9d4.png)

Open Order Screen  
![Imgur](https://i.imgur.com/ZP09z3B.png)

Context menu to add a Position symbol to any defined Bot  
![Imgur](https://i.imgur.com/KQvGIRi.png)


List of Closed Order  
![Imgur](https://i.imgur.com/KCeNEk8.png)

Position Watchlist  
![Imgur](https://i.imgur.com/XXvzkRg.png)

Context menu to add a symbol to any defined Bot from watchlist  
![Imgur](https://i.imgur.com/coNxk4v.png)

Scanners  
![Imgur](https://i.imgur.com/5cdraN1.png)

Context menu to add a symbol to any defined Bot from scanner list  
![Imgur](https://i.imgur.com/6VXCOa0.png)

Bots  
![Imgur](https://i.imgur.com/L1HRD59.png)

Option to start, end or delete a Bot for a selected sysmbol  
![Imgur](https://i.imgur.com/CKCOqAq.png)

Scanner class should implement this Interface  
![Imgur](https://i.imgur.com/PZOUj45.png)

Bot classes should implement this interface  
![Imgur](https://i.imgur.com/JjXiCMO.png)

Currently 3 scanner are implemented
1. ScannerCrypto is a simple scanner which list the Cryto Asset.  
2. ScannerAboveVolumne make use of SnapShot to filter asset between the given Close price range and above the given Volume.  
3. ScannerAboveSMA make use of SnapShot to filter asset between the given Close price range and above the given Volume and use OoplesFinance.StockIndicator to select stock above simple moving average, this scanner alos have a logi to refresh scanning every given intervals.  

These scanner symbols are stored in the Alpca Account as watchlist, so these symbols will get reloaded next time you start this dashboard.  


Currently 3 Bots are implemented
1. MeanReversion: 
2. TakeProfitLoss: places a bracket order when the close is above SMA.  
3. Scalper: Places a market order when close is above SMA and sells the asset as soon as you make a certain profit amount.  

These Bots and Scanner as sample implementation, its up to you write new once that make sense to you, the dashboard provide a frame work to handle position and orders and implement scanner or bots.

You will need some knowlege of C# to write these Scanner or Bots.

Check This project @ https://github.com/tcj2001/AlpacaDashboard-DotNet-6.0

Thanks to cheatcountry who helped me optimize some of the code in this project, check his works on OoplesFinance.StockIndicators @ https://github.com/ooples/OoplesFinance.StockIndicators 
 

