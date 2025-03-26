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
    private readonly BinanceSocketClient _socketClient = new();
    private readonly IHubContext<BinanceHub> _hubContext;
    private readonly ConcurrentDictionary<string, UpdateSubscription> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _symbolConnections = new();
    private readonly ConcurrentDictionary<string, UpdateSubscription> _orderBookSubscriptions = new();

    public LastPriceBackgroundService(IHubContext<BinanceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var binanceClient = new BinanceSocketClient();
    }
    
    public async Task AddSubscription(string connectionId, string symbol)
    {
        _symbolConnections.AddOrUpdate(symbol,
            new HashSet<string> { connectionId },
            (_, set) => { set.Add(connectionId); return set; });

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
    
    public async Task SubscribeToOrderBook(string connectionId, string symbol, int levels = 10)
    {
        _symbolConnections.AddOrUpdate($"OB_{symbol}",
            new HashSet<string> { connectionId },
            (_, set) => { set.Add(connectionId); return set; });

        if (!_orderBookSubscriptions.ContainsKey(symbol))
        {
            var subscription = await _socketClient.SpotApi.ExchangeData
                .SubscribeToOrderBookUpdatesAsync(symbol, 1000, data =>
                {
                    var book = data.Data;
                    _hubContext.Clients.Group($"ORDERBOOK_{symbol}").SendAsync("OrderBookUpdate", new {
                        Symbol = symbol,
                        levels = levels,
                        Bids = book.Bids.Take(levels),
                        Asks = book.Asks.Take(levels),
                        LastUpdateId = book.LastUpdateId
                    });
                });

            _orderBookSubscriptions[symbol] = subscription.Data;
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
    
    public async Task Unsubscribe(string connectionId, string symbol)
    {
        await RemoveFromGroup(connectionId, symbol, _activeSubscriptions, $"PRICE_{symbol}");
        await RemoveFromGroup(connectionId, symbol, _orderBookSubscriptions, $"ORDERBOOK_{symbol}");
    }
    
    private async Task RemoveFromGroup(string connectionId, string symbol, 
        ConcurrentDictionary<string, UpdateSubscription> subscriptions, string groupPrefix)
    {
        var groupKey = groupPrefix.StartsWith("PRICE") ? symbol : $"OB_{symbol}";
        
        if (_symbolConnections.TryGetValue(groupKey, out var connections))
        {
            connections.Remove(connectionId);
            
            if (connections.Count == 0)
            {
                if (subscriptions.TryRemove(symbol, out var subscription))
                {
                    await subscription.CloseAsync();
                }
                _symbolConnections.TryRemove(groupKey, out _);
            }
        }
    }
}