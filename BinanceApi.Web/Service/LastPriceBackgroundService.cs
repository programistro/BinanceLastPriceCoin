using System.Collections.Concurrent;
using Binance.Net.Clients;
using Binance.Net.Enums;
using BinanceApi.Web.Hubs;
using BinanceApi.Web.Models;
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
    private readonly ConcurrentDictionary<string, List<Kline>> _historicalKlines = new();
    private readonly KlineInterval[] _monitoredIntervals = new[]
    {
        KlineInterval.OneMinute,
        KlineInterval.FiveMinutes,
        KlineInterval.FifteenMinutes,
        KlineInterval.OneHour,
        KlineInterval.OneDay
    };
    
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
                    var ticker = _restClient.SpotApi.ExchangeData.GetTickerAsync(symbol).Result;
                    var price = data.Data.LastPrice;
                    _hubContext.Clients.Group(symbol)
                        .SendAsync("ReceivePriceUpdate", new { Symbol = symbol, Price = price });
                });

            _activeSubscriptions[symbol] = subscription.Data;
        }
    }
    
    public async Task<List<Kline>> GetKlinesForSymbol(string symbol, KlineInterval interval, int limit = 500)
    {
        var klinesResult = await _restClient.SpotApi.ExchangeData.GetKlinesAsync(
            symbol, 
            interval, 
            limit: limit);

        if (!klinesResult.Success) 
            return new List<Kline>();

        return klinesResult.Data.Select(k => new Kline
        {
            OpenTime = k.OpenTime,
            CloseTime = k.CloseTime,
            OpenPrice = k.OpenPrice,
            HighPrice = k.HighPrice,
            LowPrice = k.LowPrice,
            ClosePrice = k.ClosePrice,
            Volume = k.Volume,
            QouteVolume = k.QuoteVolume,
            TakerBuyBaseAssetVolume = k.TakerBuyBaseVolume,
            TakerBuyQuoteAssetVolume = k.TakerBuyQuoteVolume
        }).ToList();
    }
    
    public async Task InitializeSymbolKlines(string symbol)
    {
        if (_historicalKlines.ContainsKey(symbol))
            return;

        var allKlines = new List<Kline>();
        
        foreach (var interval in _monitoredIntervals)
        {
            var klines = await GetKlinesForSymbol(symbol, interval);
            allKlines.AddRange(klines);
        }

        _historicalKlines[symbol] = allKlines;
    }

    private async Task UpdateKlinesForSymbol(string symbol)
    {
        if (!_historicalKlines.TryGetValue(symbol, out var existingKlines))
            return;

        var updatedKlines = new List<Kline>();
        
        foreach (var interval in _monitoredIntervals)
        {
            var newKlines = await GetKlinesForSymbol(symbol, interval, limit: 1);
            
            // Обновляем только если появились новые данные
            var lastExisting = existingKlines
                .Where(k => k.CloseTime > DateTime.UtcNow.AddMinutes(-5))
                .OrderByDescending(k => k.CloseTime)
                .FirstOrDefault();
            
            if (newKlines.Any() && (lastExisting == null || newKlines[0].CloseTime > lastExisting.CloseTime))
            {
                updatedKlines.AddRange(newKlines);
            }
        }

        if (updatedKlines.Any())
        {
            _historicalKlines[symbol] = existingKlines
                .Where(k => k.CloseTime < DateTime.UtcNow.AddDays(-7)) // Храним неделю истории
                .Concat(updatedKlines)
                .OrderBy(k => k.CloseTime)
                .ToList();

            await _hubContext.Clients.Group($"KLINES_{symbol}").SendAsync("KlinesUpdate", 
                _historicalKlines[symbol]
                    .OrderByDescending(k => k.CloseTime)
                    .Take(1000) // Лимит для отправки
                    .ToList());
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