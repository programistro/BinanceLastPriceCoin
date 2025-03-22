using Binance.Net.Clients;
using Microsoft.AspNetCore.Mvc;

namespace BinanceApi.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class ApiController : ControllerBase
{
    private readonly ILogger<ApiController> _logger;

    public ApiController(ILogger<ApiController> logger)
    {
        _logger = logger;
    }

    [HttpGet("get-cost-btc")]
    public async Task<IActionResult> GetBtc()
    {
        var restClient = new BinanceRestClient();
        var tickerResult = await restClient.SpotApi.ExchangeData.GetTickerAsync("ETHUSDT");
        var lastPrice = tickerResult.Data.LastPrice;
        
        return Ok(lastPrice);
    }
    
    [HttpGet("get-price-btc2")]
    public async Task<IActionResult> Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            Ok(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            
            return BadRequest();
        }
        
        return Ok();
    }
}