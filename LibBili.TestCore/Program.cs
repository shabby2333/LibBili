using LibBili.Danmaku.Interface;
using LibBili.Danmaku.Model;
using System;
using System.Threading.Tasks;


Console.Write("请输入房间号：");
var roomID = Convert.ToInt64(Console.ReadLine());
//var roomID = 6;
//var roomID1 = 7734200;
var ws = new WebsocketDanmakuClient(roomID);
ws.UpdatePopularity += (sender, e) => Console.Title = $"热度: {e}";
ws.ReceiveDanmaku += (sender, e) =>
    Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]  {(e.Danmaku.IsAdmin ? "[房]" : "")}{(e.Danmaku.IsVIP ? "[爷]" : "")}{e.Danmaku.UserName}: {e.Danmaku.Text}");
ws.ReceiveGift += (sender, e) => Console.WriteLine($"{DateTime.Now}[{(sender as IBiliDanmakuClient).RoomID}]  {e.Gift.UserName} 赠送 {e.Gift.GiftName}*{e.Gift.GiftCount}");
ws.Connect();


//for (int i = 0; i < 100; i++)
//{
//    ws = new WebsocketDanmakuClient(roomID, roomID1);
//    ws.Connect();

//    await Task.Delay(100);

//    Console.WriteLine(i);
//}
Console.ReadLine();