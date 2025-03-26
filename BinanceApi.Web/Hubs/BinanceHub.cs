﻿using Binance.Net.Clients;
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
}