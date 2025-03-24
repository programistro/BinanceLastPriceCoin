using Binance.Net.Clients;
using Binance.Net.Interfaces;
using BinanceApi.Web.Models;
using BinanceApi.Web.Service;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web.Hubs;

public class BinanceHub(LastPriceBackgroundService lastPriceBackgroundService) : Hub
{
    private BinanceRestClient _restClient = new ();

    public async Task GetLastPrice(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, symbol);
        
        await lastPriceBackgroundService.SubscribeToTicker(symbol);
        
        var tickerResult = await _restClient.SpotApi.ExchangeData.GetTickerAsync(symbol);
        await Clients.Caller.SendAsync("ReceivePriceUpdate", new { 
            Symbol = symbol, 
            Price = tickerResult.Data.LastPrice 
        });
    }
    
    public async Task UnsubscribeFromPriceUpdates(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, symbol);
    }
}