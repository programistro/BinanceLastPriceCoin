using BinanceApi.Web;
using BinanceApi.Web.Hubs;
using BinanceApi.Web.Models;
using BinanceApi.Web.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHostedService<LastPriceBackgroundService>();
builder.Services.AddHostedService<LastPriceCoinBackgroundService>();
builder.Services.AddBinance();
builder.Services.AddSingleton<IBinanceDataProvider, BinanceDataProvider>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseCors(policyBuilder =>
// {
//     policyBuilder.WithOrigins("http://localhost:63342")
//         .AllowAnyMethod()
//         .AllowAnyHeader();
// });

app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHub<BinanceHub>("/lastPrice");

app.Run();