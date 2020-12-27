using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Sockets;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Blazor.ServerSide.Pages
{


    public partial class Index : ComponentBase
    {
        private IEnumerable<IBinanceTick> _ticks = new List<IBinanceTick>();
        private UpdateSubscription _subscription;
        private UpdateSubscription _subscriptionKline;
        private IEnumerable<IBinanceKline> _Klines = new List<IBinanceKline>();
        //private IEnumerable<IBinanceKline> _KlinesClosed = new List<IBinanceKline>();
        private IEnumerable<Tuple<string, IBinanceKline>> _KlineSymbol = new List<Tuple<string, IBinanceKline>>();
        //kcprivate string SOCKET = "wss://stream.binance.com:9443/ws/ethusdt@kline_1m";
        private int RSI_PERIOD = 14;
        private int RSI_OVERBOUGHT = 70;
        private int RSI_OVERSOLD = 30;
        private string TRADE_SYMBOL = "BTCUSDT";
        private double TRADE_QUANTITY = 0.05;

        IEnumerable<IBinanceKline> closes = default(IEnumerable<IBinanceKline>);
        bool inposition = false;

        string message = string.Empty;
        string message2 = string.Empty;

        DateTime ServerTime = default(DateTime);

        Decimal RSI = 0;

        int closeCount = 0;
        private IEnumerable<decimal> _closePrices;

        public IBinanceStreamKlineData LastKline { get; private set; }

        protected override async Task OnInitializedAsync()
        {
            var callResult = await _dataProvider.Get24HPrices().ConfigureAwait(false);
            if (callResult)
                _ticks = callResult.Data;

            var subResult = await _dataProvider.SubscribeTickerUpdates(HandleTickUpdates).ConfigureAwait(false);
            if (subResult)
                _subscription = subResult.Data;

        }

        private void HandleKLineUpdates(IBinanceStreamKlineData klineData)
        {
            LastKline = klineData;
            InvokeAsync(StateHasChanged);
        }
       

        public decimal CalculateRsi(int rSI_PERIOD)
        {
            var closePrices = _Klines?.Where(x => x.CloseTime <= DateTime.UtcNow).OrderBy(o => o.CloseTime).Select(x => x.Close);

            decimal sumGain = 0;
            decimal sumLoss = 0;
            decimal aveGain = 0;
            decimal aveLoss = 0;

            //var closePricesArr = closePrices.ToArray();
            var lastPrice = closePrices.TakeLast(1).SingleOrDefault();

            foreach (var price in closePrices)
            {
                var difference = price - lastPrice;

                if (difference >= 0)
                {
                    sumGain += difference;
                }
                else
                {
                    sumLoss -= difference;
                }
            }

            var closeCount = closePrices.Count();

            aveGain = 100.0M * (sumGain / closeCount);
            aveLoss = 100.0M * (sumLoss / closeCount);

            if (aveGain == 0) return 0;

            var relativeStrength = aveLoss / aveGain;

            return 100.0M - (100.0M / (1 + relativeStrength));
        }

        private void HandleTickUpdates(IEnumerable<IBinanceTick> ticks)
        {
            RenderServerTime();

            message = "Received message";

            var callKLinesResult = _dataProvider.GetKlinesAsync(TRADE_SYMBOL, KlineInterval.OneMinute).ConfigureAwait(false).GetAwaiter().GetResult();

            if (callKLinesResult)
            {
                _Klines = callKLinesResult.Data;

                if (callKLinesResult.Data.Count() >= RSI_PERIOD)
                {
                    RSI = CalculateRsi(RSI_PERIOD);

                    if (inposition)
                    {
                        // Overbought! Sell!

                        // put binance sell logic here


                    }
                    else
                    {
                        // It is overbought, but we don't own any. Nothing to do.
                    }
                }

            }



            var lines = _Klines.Count();

            if (closeCount > RSI_PERIOD)
            {
                message2 = $"RSI_PERIOD Reached {lines}";

            }
            else
            {
                message2 = $"RSI_PERIOD {lines}";

            }

            foreach (var tick in ticks)
            {
                //var callKLinesResult1 = _dataProvider.GetKlinesAsync(TRADE_SYMBOL).ConfigureAwait(false).GetAwaiter().GetResult();
                //if (callKLinesResult1)
                //    _KlineSymbol = callKLinesResult1.Data.Select(x => new Tuple<string, IBinanceKline>(tick.Symbol,x));

                var symbol = _ticks.Single(t => t.Symbol == tick.Symbol);
                symbol.PriceChangePercent = tick.PriceChangePercent;
            }

            InvokeAsync(StateHasChanged);
        }

        private void RenderServerTime()
        {
            using (var client = new BinanceClient())
            {
                var result = client.Spot.System.GetServerTime();
                if (result.Success)
                {
                    ServerTime = result.Data;
                    Console.WriteLine($"Server time: {ServerTime}");
                }
                else
                    Console.WriteLine($"Error: {result.Error}");
            }

        }

        public async ValueTask DisposeAsync()
        {
            await _dataProvider.Unsubscribe(_subscription);
        }
    }

}
