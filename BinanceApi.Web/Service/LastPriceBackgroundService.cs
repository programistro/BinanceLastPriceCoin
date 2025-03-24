using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, UpdateSubscription> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _symbolConnections = new();

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
        }, stoppingToken);
    }
    
    public async Task AddSubscription(string connectionId, string symbol)
    {
        // Добавляем подключение к списку для этого символа
        _symbolConnections.AddOrUpdate(symbol,
            new HashSet<string> { connectionId },
            (_, set) => { set.Add(connectionId); return set; });

        // Если подписка на этот символ еще не существует - создаем
        if (!_activeSubscriptions.ContainsKey(symbol))
        {
            var binanceClient = new BinanceSocketClient();
            var subscription = await binanceClient.SpotApi.ExchangeData
                .SubscribeToTickerUpdatesAsync(symbol, data =>
                {
                    var price = data.Data.LastPrice;
                    _hubContext.Clients.Group(symbol)
                        .SendAsync("ReceivePriceUpdate", new { Symbol = symbol, Price = price });
                });

            _activeSubscriptions[symbol] = subscription.Data;
        }
    }
    
    public async Task RemoveSubscription(string connectionId, string symbol)
    {
        if (_symbolConnections.TryGetValue(symbol, out var connections))
        {
            connections.Remove(connectionId);
            
            // Если больше нет подключений для этого символа - отписываемся
            if (connections.Count == 0)
            {
                if (_activeSubscriptions.TryRemove(symbol, out var subscription))
                {
                    await subscription.CloseAsync();
                }
                _symbolConnections.TryRemove(symbol, out _);
            }
        }
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
}