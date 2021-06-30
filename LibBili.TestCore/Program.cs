using LibBili.Danmaku.Interface;
using LibBili.Danmaku.Model;
using System;

System.Console.Write("请输入房间号：");
var roomID = Convert.ToInt64(Console.ReadLine());
var ws = new WebsocketDanmakuClient(roomID);
//var ws = new WebsocketDanmakuClient(6);
//ws.Open += (sender, e) => Console.WriteLine("Open");
ws.UpdatePopularity += (sender, e) => Console.Title = $"热度: {e}";
            
//ws.ReceiveNotice += (sender, e) => Console.WriteLine(e.RawMessage);
ws.ReceiveDanmaku += (sender, e) => 
    Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]  {(e.Danmaku.IsAdmin ? "[房]" : "")}{(e.Danmaku.IsVIP ? "[爷]" : "")}{e.Danmaku.UserName}: {e.Danmaku.Text}");
ws.ReceiveGift += (sender, e) => Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]  {e.Gift.UserName} 赠送 {e.Gift.GiftName}*{e.Gift.GiftCount}");
ws.Connect();

Console.ReadLine();