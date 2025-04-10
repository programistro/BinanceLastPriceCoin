using BinanceApi.Web;
using BinanceApi.Web.Hubs;
using BinanceApi.Web.Models;
using BinanceApi.Web.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(); 
builder.Services.AddHostedService<LastPriceBackgroundService>();
builder.Services.AddSingleton<LastPriceBackgroundService>();
builder.Services.AddBinance();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.UseStaticFiles();

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHub<BinanceHub>("/connectHub");

app.Run();