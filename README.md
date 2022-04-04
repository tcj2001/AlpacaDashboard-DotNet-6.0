# Alpaca Dashboard (.NET 6.0 C#)
![Imgur](https://i.imgur.com/7k2MXsY.png)  

This is Alpaca Dashboard which will allow you to manage your portfolio in Live and Paper Environment. It will list your open position, open orders, closed orders and watchlist of open positions, these list will show real time updates on the close price, position, market value, profit/loss, bid price and ask price for each assets.
Account data like buying power will updated real-time. You will also have the ability to submit new order or close position etc.  

You can build your own Scanner and Bots classes easily by implementing its interfaces. each implemented scanner will be automatically displayed in UI along with its input parameters.

Portfolio screen showing open position for the selected environment.    
![Imgur](https://i.imgur.com/y3wP9d4.png)  

Open Order Screen.  
![Imgur](https://i.imgur.com/ZP09z3B.png)  

Context menu to add a Position symbol to any defined Bot.  
![Imgur](https://i.imgur.com/KQvGIRi.png)  

List of Closed Order.    
![Imgur](https://i.imgur.com/KCeNEk8.png)  

Position Watchlist.    
![Imgur](https://i.imgur.com/XXvzkRg.png)  

Context menu to add a symbol to any defined Bot from watchlist.  
![Imgur](https://i.imgur.com/coNxk4v.png)  

Scanners, this tab will list all the scanners classes implemented based on the IScanner interface.  
![Imgur](https://i.imgur.com/5cdraN1.png)  

Context menu to add a symbol to any defined Bot from scanner list  
![Imgur](https://i.imgur.com/6VXCOa0.png)  

Scanner class should implement this Interface  
![Imgur](https://i.imgur.com/PZOUj45.png)  

Bots, this tab will list all the scanners classes implemented based on the IBots interface.    
![Imgur](https://i.imgur.com/OXd6hVz.png) 

Option to start, end or delete a Bot for a selected sysmbol  
![Imgur](https://i.imgur.com/QjvzoMP.png)

Bot classes should implement this interface  
![Imgur](https://i.imgur.com/JjXiCMO.png)  

Currently 3 scanners are implemented.  
1. ScannerCrypto is a simple scanner which list the Crypto Asset.  
2. ScannerAboveVolume make use of Snapshot to filter asset between the given Close price range and above the given Volume.  
3. ScannerAboveSMA make use of Snapshot to filter asset between the given Close price range and above the given Volume and uses OoplesFinance.StockIndicator to select stock above simple moving average, this scanner also have a logic to run scanner every given intervals.  

These scanner symbols are stored in the Alpaca Account as watchlist, so these scanner will get reloaded with last scanned list next time you start this dashboard.  

Currently 4 Bots are implemented.  
1. MeanReversion:
2. TakeProfitLoss: places a bracket order when the close is above SMA to take profit or loss at the entered percentages.  
3. Scalper: Places a market order when close is above SMA and sells the asset as soon as you make a certain profit amount.  
4. DCA (Dollar Cost Averaging):  This bot show the concept of implementing DCA to add position when the loss is more that a set percentage

For every asset added in the dashboard, a Stock object is created which updates itself with Quote, Trades, Positions, and Orders  
Bots make use of this Stock object to build its logic.

These Bots and Scanner as sample implementation, it's up to you to write new one that make sense to you, the dashboard provide a frame work to handle position and orders and implement scanner or bots.
You will need some knowlege of C# to write these Scanner or Bots.

API keys and SecretKey can provided directly in the appsetting.json  
or store your key using dotnet user-secret as
PaperKey:API_SECRET = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
PaperKey:API_KEY = xxxxxxxxxxxxxxxxxxxx
LiveKey:API_SECRET =  "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
LiveKey:API_KEY = xxxxxxxxxxxxxxxxxxxx 

A **Log** is created for the Dashboard in the Logs folder, also individual **logs** are created for each bots and asset combination.  

All trades are recorded in a **SQLite database** for price analysis like profit/loss by Date/Bot/Symbol etc.  

Setting for each Scanners are stored in the database, so next time you load the dashboard those setting will be restored.  

Setting for each Bot at Asset level is also stored in the database, so next time you load the dashboard setting for each Bots at Asset level will be restored.  

This Dashboard should also work for non-subscribed users by changing the subscribed setting to false in the appsetting.json file.  

Provide your feedback to improve the functionality of Alpaca Dashboard, **write new scanner and bots** and add to project.    

Check This project @ https://github.com/tcj2001/AlpacaDashboard-DotNet-6.0

Thanks to cheatcountry who helped me optimize some of the code in this project, check his work of OoplesFinance.StockIndicators @ https://github.com/ooples/OoplesFinance.StockIndicators 
 

