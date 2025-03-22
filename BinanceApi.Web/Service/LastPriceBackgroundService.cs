using Binance.Net.Clients;
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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Получаем цену BTCUSDT
                var tickerResult = await _restClient.SpotApi.ExchangeData.GetTickerAsync("BTCUSDT", stoppingToken);
                if (tickerResult.Success)
                {
                    var lastPrice = tickerResult.Data.LastPrice;

                    // Отправляем цену всем клиентам через SignalR
                    await _hubContext.Clients.All.SendAsync("ReceivePrice", lastPrice, cancellationToken: stoppingToken);
                }
                else
                {
                    Console.WriteLine("Ошибка при получении данных: " + tickerResult.Error?.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }

            // Ожидаем 1 секунду перед следующим запросом
            await Task.Delay(1000, stoppingToken);
        }
    }
}