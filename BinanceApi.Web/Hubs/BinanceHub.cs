using Binance.Net.Clients;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web.Hubs;

public class BinanceHub : Hub
{
    private BinanceRestClient _restClient = new ();
    
    public async Task GetLastPrice()
    {
        var tickerResult = await _restClient.SpotApi.ExchangeData.GetTickerAsync("BTCUSDT");
        var lastPrice = tickerResult.Data.LastPrice;
        
        await this.Clients.All.SendAsync("GetLastPrice", lastPrice);
    }
    
    public async Task Send(string message, string userName)
    {
        await Clients.All.SendAsync("Receive", message, userName);
    }
}