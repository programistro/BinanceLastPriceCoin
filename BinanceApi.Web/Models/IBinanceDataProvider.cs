using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;

namespace BinanceApi.Web.Models;

public interface IBinanceDataProvider
{
    Task<WebCallResult<IEnumerable<IBinanceTick>>> Get24HPrices(IEnumerable<string> tickers, CancellationToken ct);

    Task<CallResult<UpdateSubscription>> SubscribeTickerUpdates(
        Action<DataEvent<IEnumerable<IBinanceTick>>> tickHandler);

    Task Unsubscribe(UpdateSubscription subscription);

    Task SendTickerUpdate(IEnumerable<IBinanceTick> ticks);
}