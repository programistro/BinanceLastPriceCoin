using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients;
using BinanceApi.Web.Hubs;
using BinanceApi.Web.Models;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web;

public class BinanceDataProvider : IBinanceDataProvider
{
    private IBinanceRestClient _client;
    private IBinanceSocketClient _socketClient;
    private IHubContext<BinanceHub> _hubContext;
    
    private IEnumerable<IBinanceTick> _ticks;
    private UpdateSubscription _subscription;

    public BinanceDataProvider(IBinanceRestClient client, IBinanceSocketClient socketClient, IHubContext<BinanceHub> hubContext)
    {
        _client = client;
        _socketClient = socketClient;
        _hubContext = hubContext;
    }

    public Task<WebCallResult<IEnumerable<IBinanceTick>>> Get24HPrices(IEnumerable<string> tickers, CancellationToken cancellationToken = default)
    {
        return _client.SpotApi.ExchangeData.GetTickersAsync(tickers, cancellationToken);
    }

    public Task<CallResult<UpdateSubscription>> SubscribeTickerUpdates(Action<DataEvent<IEnumerable<IBinanceTick>>> tickHandler)
    {
        return _socketClient.SpotApi.ExchangeData.SubscribeToAllTickerUpdatesAsync(tickHandler);
    }

    public async Task Unsubscribe(UpdateSubscription subscription)
    {
        await _socketClient.UnsubscribeAsync(subscription);
    }
    
    public async Task SendTickerUpdate(IEnumerable<IBinanceTick> ticks)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveTickerUpdate", ticks);
    }
}