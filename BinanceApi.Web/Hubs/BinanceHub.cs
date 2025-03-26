using Binance.Net.Clients;
using Binance.Net.Enums;
using BinanceApi.Web.Service;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web.Hubs;

public class BinanceHub : Hub
{
    private BinanceRestClient _restClient = new ();
    
    private readonly LastPriceBackgroundService _lastPriceBackgroundService;

    public BinanceHub(LastPriceBackgroundService lastPriceBackgroundService)
    {
        _lastPriceBackgroundService = lastPriceBackgroundService;
    }

    public async Task GetLastPrice(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, symbol);
        
        await _lastPriceBackgroundService.SubscribeToTicker(symbol);
        
        var tickerResult = await _restClient.SpotApi.ExchangeData.GetTickerAsync(symbol);
    }
    
    public async Task SubscribeOrderBook(string symbol, int levels)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ORDERBOOK_{symbol}");
        await _lastPriceBackgroundService.SubscribeToOrderBook(Context.ConnectionId, symbol, levels);
        
        var orderBook = await _restClient.SpotApi.ExchangeData.GetOrderBookAsync(symbol, levels);
        await Clients.Caller.SendAsync("OrderBookUpdate", new {
            Symbol = symbol,
            Bids = orderBook.Data.Bids,
            Asks = orderBook.Data.Asks,
            LastUpdateId = orderBook.Data.LastUpdateId
        });
    }

    public async Task SubscribeToPriceUpdates(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, symbol);
        
        await _lastPriceBackgroundService.AddSubscription(Context.ConnectionId, symbol);
        
        var tickerResult = await _restClient.SpotApi.ExchangeData.GetTickerAsync(symbol);
        await Clients.Caller.SendAsync("ReceivePriceUpdate", new 
        { 
            Symbol = symbol, 
            Price = tickerResult.Data.LastPrice 
        });
    }

    public async Task UnsubscribeFromPriceUpdates(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, symbol);
        await _lastPriceBackgroundService.RemoveSubscription(Context.ConnectionId, symbol);
    }
    
        public async Task SubscribeToKlines(string symbol)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"KLINES_{symbol}");
            
            // Инициализируем данные при первой подписке
            await _lastPriceBackgroundService.InitializeSymbolKlines(symbol);
            
            // Получаем текущие данные
            var klines = await _lastPriceBackgroundService.GetKlinesForSymbol(symbol, KlineInterval.OneDay, 100);
            await Clients.Caller.SendAsync("InitialKlines", klines);
        }
    
        public async Task UnsubscribeFromKlines(string symbol)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"KLINES_{symbol}");
        }
    
        public async Task RequestKlines(string symbol, string interval, int limit)
        {
            var klineInterval = Enum.Parse<KlineInterval>(interval);
            var klines = await _lastPriceBackgroundService.GetKlinesForSymbol(symbol, klineInterval, limit);
            await Clients.Caller.SendAsync("KlinesResponse", klines);
        }
}