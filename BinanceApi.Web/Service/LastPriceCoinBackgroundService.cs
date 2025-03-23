using Binance.Net.Clients;
using Binance.Net.Enums;
using BinanceApi.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BinanceApi.Web.Service;

public class LastPriceCoinBackgroundService : BackgroundService
{
    private readonly BinanceRestClient _restClient = new();
    private readonly IHubContext<BinanceHub> _hubContext;

    public LastPriceCoinBackgroundService(IHubContext<BinanceHub> hubContext)
    {
        _hubContext = hubContext;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var binanceClient = new BinanceSocketClient();
        
        var symbol = "BTCUSDT";
        var endTime = DateTime.UtcNow;
        var interval = KlineInterval.OneMinute;

        while (true)
        {
            var klinesResult = await binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
                symbol,
                interval,
                endTime.AddMinutes(-15),
                endTime);
        
            var klines = klinesResult.Data.Result.ToList();
        
            var priceNow = klines.Last().ClosePrice;
            var price5MinAgo = klines[klines.Count - 6].ClosePrice;
            var price10MinAgo = klines[klines.Count - 11].ClosePrice;
            var price15MinAgo = klines.First().ClosePrice;

            // Рассчитываем изменение цены в процентах
            var change5Min = CalculatePriceChange(priceNow, price5MinAgo);
            var change10Min = CalculatePriceChange(priceNow, price10MinAgo);
            var change15Min = CalculatePriceChange(priceNow, price15MinAgo);

            // Отправляем данные клиенту
            await _hubContext.Clients.All.SendAsync("ReceivePriceChanges", new
            {
                Change5Min = change5Min,
                Change10Min = change10Min,
                Change15Min = change15Min
            });
            
            await Task.Delay(1000, stoppingToken);
        }
    }
    
    private decimal CalculatePriceChange(decimal currentPrice, decimal previousPrice)
    {
        if (previousPrice == 0)
            return 0;

        return ((currentPrice - previousPrice) / previousPrice) * 100;
    }
}