using Binance.Net.Clients;
using Binance.Net.Enums;
using BinanceApi.Web.Hubs;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web.Service;

public class LastPriceBackgroundService : BackgroundService
{
    private readonly BinanceRestClient _restClient = new();
    private readonly IHubContext<BinanceHub> _hubContext;
    private readonly Dictionary<string, UpdateSubscription> _activeSubscriptions = new();

    public LastPriceBackgroundService(IHubContext<BinanceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var binanceClient = new BinanceSocketClient();

        await binanceClient.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync("BTCUSDT", data =>
        {
            var price = data.Data.LastPrice;
            _hubContext.Clients.All.SendAsync("ReceivePriceUpdate", price);
            
            var klinesResult = binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
                "BTCUSDT",
                KlineInterval.OneMinute,
                DateTime.UtcNow.AddMinutes(-15),
                DateTime.UtcNow).Result;
        
            var klines = klinesResult.Data.Result.ToList();
        
            var priceNow = klines.Last().ClosePrice;
            var price5MinAgo = klines[klines.Count - 6].ClosePrice;
            var price10MinAgo = klines[klines.Count - 11].ClosePrice;
            var price15MinAgo = klines.First().ClosePrice;

            // var change5Min = CalculatePriceChange(priceNow, price5MinAgo);
            // var change10Min = CalculatePriceChange(priceNow, price10MinAgo);
            // var change15Min = CalculatePriceChange(priceNow, price15MinAgo);

            _hubContext.Clients.All.SendAsync("ReceivePriceChanges", new
            {
                Change5Min = price5MinAgo,
                Change10Min = price10MinAgo,
                Change15Min = price15MinAgo
            });
        }, stoppingToken);
    }
    
    public async Task SubscribeToTicker(string symbol)
    {
        if (_activeSubscriptions.ContainsKey(symbol))
            return;

        var binanceClient = new BinanceSocketClient();
        var subscription = await binanceClient.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync(symbol, data =>
        {
            var price = data.Data.LastPrice;
            _hubContext.Clients.Group(symbol).SendAsync("ReceivePrice", price);
        });

        _activeSubscriptions[symbol] = subscription.Data;
    }

    public async Task UnsubscribeFromTicker(string symbol)
    {
        if (_activeSubscriptions.TryGetValue(symbol, out var subscription))
        {
            await subscription.CloseAsync();
            _activeSubscriptions.Remove(symbol);
        }
    }
}