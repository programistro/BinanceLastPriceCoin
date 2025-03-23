using Binance.Net.Clients;
using Binance.Net.Interfaces;
using BinanceApi.Web.Models;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web.Hubs;

public class BinanceHub : Hub
{
    private BinanceRestClient _restClient = new ();
    
    private readonly IBinanceDataProvider _dataProvider;

    private IEnumerable<IBinanceTick> _ticks = new List<IBinanceTick>();
    private UpdateSubscription _subscription;
    
    public BinanceHub(IBinanceDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }
    
    public async Task GetLastPrice()
    {
        var tickerResult = await _restClient.SpotApi.ExchangeData.GetTickerAsync("BTCUSDT");
        var lastPrice = tickerResult.Data.LastPrice;
        
        await this.Clients.All.SendAsync("GetLastPrice", lastPrice);
    }
}