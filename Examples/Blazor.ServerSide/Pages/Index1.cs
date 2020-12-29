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
        private int RSI_PERIOD = 14;  // must be even number
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


        public static decimal StdDev(IEnumerable<decimal> values)
        {
            // ref: http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/
            double mean = 0.0;
            double sum = 0.0;
            double stdDev = 0.0;
            int n = 0;
            foreach (double val in values)
            {
                n++;
                double delta = val - mean;
                mean += delta / n;
                sum += delta * (val - mean);
            }
            if (1 < n)
                stdDev = Math.Sqrt(sum / (n - 1));

            return (decimal)stdDev;
        }

        public decimal CalculateRsi()
        {
            decimal sumGain = 0;
            decimal sumLoss = 0;

            var lastPrice = _Klines.TakeLast(1).SingleOrDefault().Close;
            var closeCount = _Klines.Count();
            //var closePriceFloatArr = _Klines.Select(x => (float)x.Close).ToArray();
            //var closePriceDoubleArr = _Klines.Select(x => (double)x.Close).ToArray();
            var counter1 = 0;

            // average closing price in RSI_PERIOD
            var averageClosingPrice = _Klines.Average(x => x.Close);
            var averageClosingDifferance = _Klines.Average(x => Math.Abs(x.Close - lastPrice));
            var sumClosingDifferance = _Klines.Sum(x => x.Close - lastPrice);
            var orderedClosingDifferance = _Klines.OrderBy(x => x.Close - lastPrice);
            var orderedClosing = _Klines.OrderBy(x => x.Close);

            var minClosingPrice = orderedClosing.First();
            var maxClosingPrice = orderedClosing.Last();

            var median = orderedClosing.ElementAt(closeCount / 2).Close + orderedClosing.ElementAt((closeCount - 1) / 2).Close;
            median /= 2;

        //    var sumClosingScqures = _Klines.Sum(x => Math.Pow((double)x.Close, 2));

            // calculate mean and standard deviation while processing one number at a time

            var stddev = StdDev(_Klines.Select(x => x.Close));

            var _KlinesArr = _Klines.ToArray();

            var averageGain = 0.0M;
            var averageLoss = 0.0M;

            var previousAverageGain = 0.0M;
            var previousaverageLoss = 0.0M;

            foreach (var price in _KlinesArr)
            {
                var currentClose = _KlinesArr[counter1].Close;

                if (counter1 == 0)
                {
                    counter1++;
                    continue;
                }

                var previouseClose = _KlinesArr[counter1 - 1].Close;

                var difference =  previouseClose - currentClose;

                if (difference >= previouseClose)
                {
                    sumGain += difference;
                }
                else
                {
                    sumLoss -= difference;
                }

                counter1++;
            }

            // step 1

            previousAverageGain = (sumGain / RSI_PERIOD) * 100;
            previousaverageLoss = (sumLoss / RSI_PERIOD) * 100;

            //var standardDeviation = Math.Sqrt((sumClosingScqures / closeCount - (double)median) * (double)median);

            if (averageLoss == 0) { return 0; }

            var relativeStrength = averageGain / averageLoss;
            var rsiToReturn = 100.0M - (100.0M / (1 + ((previousAverageGain / RSI_PERIOD)) + sumGain / -(previousaverageLoss / RSI_PERIOD) + sumLoss)); ;

            return rsiToReturn;
        }

        private void HandleTickUpdates(IEnumerable<IBinanceTick> ticks)
        {
            RenderServerTime();

            message = "Received message";
            _dataProvider.RSI_PERIOD = RSI_PERIOD;
            _dataProvider.KLinesStartTime = DateTime.UtcNow.AddMinutes(-15);
            _dataProvider.KlinesEndTime = DateTime.UtcNow.AddMinutes(-1);

            var callKLinesResult = _dataProvider.GetKlinesAsync(TRADE_SYMBOL, KlineInterval.OneDay).ConfigureAwait(false).GetAwaiter().GetResult();

            if (callKLinesResult)
            {
                _Klines = callKLinesResult.Data;

                RSI = CalculateRsi();

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
