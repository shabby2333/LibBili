using LibBili.Danmaku;
using System;

Console.Write("请输入房间号：");
var roomId = Convert.ToInt64(Console.ReadLine());
var ws = new WebsocketDanmakuClient(roomId);
ws.UpdatePopularity += (sender, e) => Console.Title = $"热度: {e}";
ws.UpdateWatched += (sender, e) => Console.Title = $"{e} 人观看过";
ws.ReceiveDanmaku += (sender, e) =>
    Console.WriteLine($"{DateTime.Now:HH:mm:ss}[{(((BiliDanmakuClient)sender)!).RoomID}][弹幕] {(e.Danmaku.IsAdmin ? "[房]" : "")}{(e.Danmaku.IsVIP ? "[爷]" : "")}{e.Danmaku.UserName}: {e.Danmaku.Text}");
ws.ReceiveGift += (sender, e) => 
    Console.WriteLine($"{DateTime.Now:HH:mm:ss}[{(((BiliDanmakuClient)sender)!).RoomID}][礼物][{e.Gift.Price}] {e.Gift.UserName} 赠送 {e.Gift.GiftName}*{e.Gift.GiftCount}");
ws.Connect();

_ = Console.ReadLine();