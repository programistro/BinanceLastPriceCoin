namespace BinanceApi.Web.Models;

public class Kline
{
    public decimal ClosePrice { get; set; }
    
    public decimal HighPrice { get; set; }
    
    public decimal LowPrice { get; set; }
    
    public decimal OpenPrice { get; set; }
    
    public decimal Volume { get; set; }
    
    public DateTime OpenTime { get; set; }
    
    public DateTime CloseTime { get; set; }
    
    public decimal QouteVolume { get; set; }
    
    public decimal TakerBuyBaseAssetVolume { get; set; }
    
    public decimal TakerBuyQuoteAssetVolume { get; set; }
}