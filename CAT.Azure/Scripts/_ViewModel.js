$(function () {

    $('#trades').hide();

    function GridViewModel(trades) {
        this.trades = ko.observableArray(trades)
    };

    function controller() {
        this.model = new GridViewModel([]);
        this.connection = $.hubConnection();
        this.connection.logging = true;
        this.siteMonitorHub = this.connection.createHubProxy("TradeHub");

        this.toggleGrid = function () {
            if ($('.trade').length == 0) {
                $('#noTradesToMonitorMessage').show();
                $('#trades').hide();
            }
            else {
                $('#noTradesToMonitorMessage').hide();
                $('#trades').show();
            }
        }

        this.addTrade = function (trade) {
            if ($('.trade[id="' + trade.Id + '"]').length == 0) {
                this.model.trades.push(format(trade));
            }
        };

        this.updTrade = function (trade) {
            var match = ko.utils.arrayFirst(this.model.trades(), function (item) {
                return item.Id == trade.Id;
            });
            if (!match) return;

            var old = this.model.trades()[this.model.trades.indexOf(match)];
            this.model.trades.replace(old, format(trade));
        };
    };

    function format(trade) {
        var formatted = trade;
        formatted.Type = trade.Type > 0 ? "C" : "V";
        formatted.EntryValue = trade.EntryValue.toFixed(2);
        formatted.EntryTime = moment(trade.EntryTime).format('DD/MM HH:mm:ss');
        formatted.Result = trade.Result === null ? null : trade.Result.toFixed(2);
        formatted.StopLoss = trade.StopLoss === null ? null : trade.StopLoss.toFixed(2);
        formatted.StopGain = trade.StopGain === null ? null : trade.StopGain.toFixed(2);
        formatted.ExitValue = trade.ExitValue === null ? null : trade.ExitValue.toFixed(2);
        formatted.NetResult = trade.NetResult === null ? null : trade.NetResult.toFixed(2);
        formatted.ExitTime = trade.ExitTime === null ? null : moment(trade.ExitTime).format('DD/MM HH:mm:ss');
        return $.extend(trade, formatted);
    };

    var c = new controller();

    c.siteMonitorHub
        .on('storedTrades', function (trades) {
            //trades.length = 0;
            $(trades).each(function (i, trade) {
                c.addTrade(trade);
            });
            c.toggleGrid();
        })
        .on('addTrade2Page', function (trade) {
            c.addTrade(trade);
            c.toggleGrid();
        })
        .on('updateTrade', function (trade) {
            c.updTrade(trade);
        });
    
    c.connection.start().done(function () {
        c.siteMonitorHub.invoke('getTradeList');
    });

    ko.bindingHandlers.sort = {
        init: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
            var asc = false;
            element.style.cursor = 'pointer';

            element.onclick = function () {
                var value = valueAccessor();
                var prop = value.prop;
                var data = value.arr;

                asc = !asc;
                if (asc) {
                    data.sort(function (left, right) {
                        return left[prop] == right[prop] ? 0 : left[prop] < right[prop] ? -1 : 1;
                    });
                } else {
                    data.sort(function (left, right) {
                        return left[prop] == right[prop] ? 0 : left[prop] > right[prop] ? -1 : 1;
                    });
                }
            }
        }
    };

    ko.applyBindings(c.model);
});