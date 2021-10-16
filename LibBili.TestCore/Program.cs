using LibBili.Danmaku;
using System;

Console.Write("请输入房间号：");
var roomID = Convert.ToInt64(Console.ReadLine());
var ws = new TcpDanmakuClient(roomID);
//for (int i = 0; i < 500; i++)
//{
//    ws = new WebsocketDanmakuClient(roomID);
//}
ws.UpdatePopularity += (sender, e) => Console.Title = $"热度: {e}";
ws.ReceiveDanmaku += (sender, e) =>
    Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]  {(e.Danmaku.IsAdmin ? "[房]" : "")}{(e.Danmaku.IsVIP ? "[爷]" : "")}{e.Danmaku.UserName}: {e.Danmaku.Text}");
ws.ReceiveGift += (sender, e) => Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]  {e.Gift.UserName} 赠送 {e.Gift.GiftName}*{e.Gift.GiftCount}");
ws.Connect();

Console.ReadLine();