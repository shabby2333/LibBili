using LibBili.Danmaku.Interface;
using LibBili.Danmaku.Model;
using System;

namespace LibBili.TestCore
{
    class Program
    {
        static void Main(string[] args)
        {
            //var ws = new WebsocketDanmakuClient(6, 7734200);
            var ws = new WebsocketDanmakuClient(16405, 16405);
            //ws.Open += (sender, e) => Console.WriteLine("Open");
            ws.UpdatePopularity += (sender, e) => Console.Title = $"Popularity:{e}";
            
            //ws.ReceiveNotice += (sender, e) => Console.WriteLine(e.RawMessage);
            ws.ReceiveDanmaku += (sender, e) => Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]\t{(e.Danmaku.IsAdmin ? "[房]" : "")}{(e.Danmaku.IsVIP ? "[爷]" : "")}{e.Danmaku.UserName}: {e.Danmaku.Text}");
            ws.ReceiveGift += (sender, e) => Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]\t{e.Gift.UserName} 赠送 {e.Gift.GiftName}*{e.Gift.GiftCount}");
            ws.Connect();

            Console.ReadLine();
        }
    }
}
