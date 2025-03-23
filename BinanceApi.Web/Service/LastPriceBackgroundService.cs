using Binance.Net.Clients;
using Binance.Net.Enums;
using BinanceApi.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web.Service;

public class LastPriceBackgroundService : BackgroundService
{
    private readonly BinanceRestClient _restClient = new();
    private readonly IHubContext<BinanceHub> _hubContext;

    public LastPriceBackgroundService(IHubContext<BinanceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var binanceClient = new BinanceSocketClient();

        var subscription = await binanceClient.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync("BTCUSDT", data =>
        {
            var price = data.Data.LastPrice;
            _hubContext.Clients.All.SendAsync("ReceivePriceUpdate", price);
        });
    }
}