using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibBili.Api.Util;
using LibBili.Danmaku.Model;
using Newtonsoft.Json.Linq;
// ReSharper disable InconsistentNaming

namespace LibBili.Danmaku
{
    public abstract class BiliDanmakuClient : IDisposable
    {
        public long RoomID { get; }
        public long? RealRoomID { get; protected set; }
        public bool Connected { get; protected set; }
        protected Timer _timer;
        protected string _token;
        protected readonly CookieContainer _cookies = new();
        protected readonly HttpClient _http;

        /// <summary>
        /// 弹幕连接建立
        /// </summary>
        public event EventHandler Open;

        // /// <summary>
        // /// 弹幕连接关闭
        // /// </summary>
        // public event EventHandler Close;

        // /// <summary>
        // /// 弹幕连接出现异常
        // /// </summary>
        // public event EventHandler<Exception> Error;

        // /// <summary>
        // /// 准备开播
        // /// </summary>
        //public event EventHandler LivePrepare;
        /// <summary>
        /// 开播
        /// </summary>
        public event EventHandler LiveStart;
        // /// <summary>
        // /// 下播
        // /// </summary>
        //public event EventHandler LiveEnd;
        // /// <summary>
        // /// 直播被切
        // /// </summary>
        //public event EventHandler LiveCut;

        /// <summary>
        /// 收到弹幕
        /// </summary>
        public event EventHandler<ReceiveDanmakuEventArgs> ReceiveDanmaku;

        /// <summary>
        /// 收到礼物
        /// </summary>
        public event EventHandler<ReceiveGiftEventArgs> ReceiveGift;

        /// <summary>
        /// 所有<code>Operation.ServerNotify</code>内容 包含头部原始数据
        /// </summary>
        public event EventHandler<ReceiveNoticeEventArgs> ReceiveNotice;

        /// <summary>
        /// 更新人气
        /// </summary>
        public event EventHandler<int> UpdatePopularity;

        /// <summary>
        /// 更新观看人数
        /// </summary>
        public event EventHandler<int> UpdateWatched;

        // /// <summary>
        // /// 更新礼物榜
        // /// </summary>
        //public event EventHandler UpdateGiftTop;
        /// <summary>
        /// 欢迎
        /// </summary>
        // public event EventHandler Welcome;
        // /// <summary>
        // /// 欢迎老爷
        // /// </summary>
        //public event EventHandler WelcomeVip;
        // /// <summary>
        // /// 欢迎船员
        // /// </summary>
        //public event EventHandler WelcomeGuard;

        // /// <summary>
        // /// 上船
        // /// </summary>
        //public event EventHandler GuardBuy;
        // /// <summary>
        // /// 
        // /// </summary>
        //public event EventHandler SuperChat;
        // /// <summary>
        // /// 观众互动消息
        // /// </summary>
        //public event EventHandler Interact;

        public BiliDanmakuClient(long roomID, long? realRoomID = null)
        {
            RealRoomID = realRoomID;
            RoomID = roomID;
            _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies });
        }

        public abstract void Connect();
        public abstract void Disconnect();
        public abstract void Dispose();
        public abstract void Send(byte[] packet);
        public abstract Task SendAsync(byte[] packet);
        public abstract void Send(Packet packet);
        public abstract Task SendAsync(Packet packet);

        protected virtual void OnOpen()
        {
            SendAsync(Packet.Authority(RealRoomID!.Value, _token));
            Connected = true;

            _timer?.Dispose();
            _timer = new Timer((e) => (
                (BiliDanmakuClient)e)?.SendAsync(Packet.HeartBeat()), this, 0, 30 * 1000);
        }

        protected void ProcessPacket(ReadOnlySpan<byte> bytes) =>
            ProcessPacketAsync(new Packet(bytes));

        protected async void ProcessPacketAsync(Packet packet)
        {
            var header = packet.Header;
            switch (header.ProtocolVersion)
            {
                case ProtocolVersion.UnCompressed:
                case ProtocolVersion.HeartBeat:
                    break;
                case ProtocolVersion.Zlib:
                    await foreach (var packet1 in ZlibDeCompressAsync(packet.PacketBody))
                        ProcessPacketAsync(packet1);
                    return;
                case ProtocolVersion.Brotli:
                    await foreach (var packet1 in BrotliDecompressAsync(packet.PacketBody))
                        ProcessPacketAsync(packet1);
                    return;
                default:
                    throw new NotSupportedException(
                        "New bilibili danmaku protocol appears, please contact the author if you see this Exception.");
            }

            switch (header.Operation)
            {
                case Operation.AuthorityResponse:
                    Open?.Emit(this, null);
                    break;
                case Operation.HeartBeatResponse:
                    Array.Reverse(packet.PacketBody);
                    var popularity = BitConverter.ToInt32(packet.PacketBody[0..4]);
                    UpdatePopularity?.Emit(this, popularity);
                    break;
                case Operation.ServerNotify:
                    ProcessNotice(Encoding.UTF8.GetString(packet.PacketBody));
                    break;
                // HeartBeat packet request, only send by client
                case Operation.HeartBeat:
                // This operation key only used for sending authority packet by client
                case Operation.Authority:
                default:
                    Console.WriteLine($"Unknown asd {packet}");
                    break;
            }
        }

        protected void ProcessNotice(string rawMessage)
        {
            var json = JObject.Parse(rawMessage);
            ReceiveNotice?.Emit(this, new ReceiveNoticeEventArgs { RawMessage = rawMessage, JsonMessage = json });
            switch (json["cmd"]?.ToString())
            {
                case "DANMU_MSG":
                    ReceiveDanmaku?.Emit(this, new ReceiveDanmakuEventArgs
                    {
                        Danmaku = new Danmaku
                        {
                            UserID = json["info"]![2]![0]!.Value<int>(),
                            UserName = json["info"][2][1]!.ToString(),
                            Text = json["info"][1]!.ToString(),
                            IsAdmin = json["info"][2][2]!.ToString() == "1",
                            IsVIP = json["info"][2][3]!.ToString() == "1",
                            UserGuardLevel = json["info"][7]!.Value<int>()
                        },
                        JsonMessage = json, RawMessage = rawMessage
                    });
                    break;
                // {"cmd": "SEND_GIFT","data": {"action": "投喂","batch_combo_id": "batch:gift:combo_id:272007008:3821157:30607:1651502756.9635","batch_combo_send": null,"beatId": "0","biz_source": "Live","blind_gift": null,"broadcast_id": 0,"coin_type": "silver","combo_resources_id": 1,"combo_send": null,"combo_stay_time": 3,"combo_total_coin": 1,"crit_prob": 0,"demarcation": 1,"discount_price": 0,"dmscore": 20,"draw": 0,"effect": 0,"effect_block": 1,"face": "http://i2.hdslb.com/bfs/face/4ba8b752b5c3aa37b5893ae04a5235e80b8f7b3d.jpg","float_sc_resource_id": 0,"giftId": 30607,"giftName": "小心心","giftType": 5,"gold": 0,"guard_level": 0,"is_first": false,"is_special_batch": 0,"magnification": 1,"medal_info": {"anchor_roomid": 0,"anchor_uname": "","guard_level": 0,"icon_id": 0,"is_lighted": 1,"medal_color": 6126494,"medal_color_border": 6126494,"medal_color_end": 6126494,"medal_color_start": 6126494,"medal_level": 7,"medal_name": "秧歌星","special": "","target_id": 3821157},"name_color": "","num": 1,"original_gift_name": "","price": 0,"rcost": 26860803,"remain": 0,"rnd": "1651502757110800002","send_master": null,"silver": 0,"super": 0,"super_batch_gift_num": 2,"super_gift_num": 2,"svga_block": 0,"tag_image": "","tid": "1651502757110800002","timestamp": 1651502757,"top_list": null,"total_coin": 0,"uid": 272007008,"uname": "醉梦衡"}}
                case "SEND_GIFT":
                    ReceiveGift?.Emit(this, new ReceiveGiftEventArgs
                    {
                        Gift = new Gift
                        {
                            GiftName = json["data"]?["giftName"]?.ToString(),
                            UserName = json["data"]?["uname"]?.ToString(),
                            UserID = json["data"]!["uid"]!.ToObject<int>(),
                            GiftCount = json["data"]["num"]!.ToObject<int>(),
                            Price = json["data"]["price"].ToObject<decimal>()
                        },
                        JsonMessage = json, RawMessage = rawMessage
                    });
                    break;
                case "LIVE":
                    LiveStart?.Emit(this, null);
                    break;
                case "WATCHED_CHANGE":
                    UpdateWatched?.Emit(this, ((int)json["data"]["num"]));
                    break;
                // {"cmd": "ROOM_REAL_TIME_MESSAGE_UPDATE","data": {"roomid": 21692711,"fans": 116970,"red_notice": -1,"fans_club": 6206}}
                case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                // {"cmd": "INTERACT_WORD","data": {"contribution": {"grade": 0},"dmscore": 16,"fans_medal": {"anchor_roomid": 2605955,"guard_level": 0,"icon_id": 0,"is_lighted": 1,"medal_color": 13081892,"medal_color_border": 13081892,"medal_color_end": 13081892,"medal_color_start": 13081892,"medal_level": 18,"medal_name": "QB个体","score": 309571,"special": "","target_id": 1474061},"identities": [3,1],"is_spread": 0,"msg_type": 1,"roomid": 21692711,"score": 1651821669636,"spread_desc": "","spread_info": "","tail_icon": 0,"timestamp": 1651502098,"trigger_time": 1651502097534739200,"uid": 8349285,"uname": "列克星敦","uname_color": ""}}
                case "INTERACT_WORD":
                //case "ONLINERANK":
                // {"cmd": "ONLINE_RANK_V2","data": {"list": [{"uid": 801016,"face": "http://i1.hdslb.com/bfs/face/9ee6482c35893220ad63e389612812327b00bdcc.jpg","score": "1205","uname": "dragonborn","rank": 1,"guard_level": 3},{"uid": 19669482,"face": "http://i0.hdslb.com/bfs/face/cff945dd822fb3c8fbd35c470844759bc52936b2.jpg","score": "1150","uname": "睡着了Qi","rank": 2,"guard_level": 2},{"uid": 2086071434,"face": "http://i2.hdslb.com/bfs/face/59d084a7689953d080d9cb7c29ea9d45854c9f9d.jpg","score": "845","uname": "陌沫WatchV","rank": 3,"guard_level": 3},{"uid": 105114477,"face": "http://i0.hdslb.com/bfs/face/member/noface.jpg","score": "825","uname": "夏小姐的单推人","rank": 4,"guard_level": 3},{"uid": 31523216,"face": "http://i2.hdslb.com/bfs/face/54235d94772bd93c4a4e959e595e09a4cadac246.jpg","score": "606","uname": "不加冰的雪碧","rank": 5,"guard_level": 3},{"uid": 13575541,"face": "http://i1.hdslb.com/bfs/face/c70b4ec37459650cadb266172e82fa4c32903c5c.jpg","score": "605","uname": "夜临星烁","rank": 6,"guard_level": 3},{"uid": 47369134,"face": "http://i1.hdslb.com/bfs/face/ea2dccce41fa233cac59022835e4fbe95b139ed7.jpg","score": "507","uname": "孤星亮","rank": 7,"guard_level": 2}],"rank_type": "gold-rank"}}
                case "ONLINE_RANK_V2":
                //case "PREPARING":
                //case "CUT":
                //case "SEND_TOP":
                //case "ROOM_RANK":
                // {"cmd": "STOP_LIVE_ROOM_LIST","data": {"room_id_list": [12130113,1888651,24775818,286843,35591,5240519,11396945,12309595,12835598,1593945,24014211,24938992,2681447,4521090,7489961,10184328,11152124,1292761,21728614,22302313,22566145,24938617,24938919,345301,3879106,5340575,11441381,11446254,11788423,15005400,22658581,23811996,24067153,24417386,24768155,24794330,24938381,581348,193146,23379717,23743874,24174612,24931613,2782947,1316415,178209,22299457,23902309,5188516,5691083,7719939,785119,9972675,13905586,14955653,24390991,24932053,5889477,9386703,23296418,23399346,23872671,24938999,13711502,24428486,24692475,24816082,24938669,704322,7201775,10134013,1336693,22988057,2920624,409846,41111,15044090,22065341,22895719,23599333,24551930,24939008,2938798,3225342,4066437,12275876,12619992,14655074,22014163,23734193,23753380,24518200,24797019,11358562,1722235,2251178,24938956,3879127,5802175,23486927,10596301,22028501,23930369,4912204,24242219,8363711,96915,21580891,21860542,23462261,24494549,24938973,3361682,9248640,23529161,24210188,24390929,3772978,22970009,23775740,23970874,24799251,24844340,340184,4917254,24544184,282032,3088803,5108708,9122313,9894536,10722,14337413,22912498,23021300,23633628,23839225,24938473,3051502,22869001,24172827,24801710,6155873,21758877,23125329,23914846,5905874,2234316,22505124,22919336,24908057,2614577,27147,3099525,4333801,5057194,12476789,13589722,23555644,24903146,5094672,7747471,23335102,23450349,7520800,7526363,21267063,23412074,23652648,24052361,24615491,24887560,380451,9200513,2326935,24918512,4181540,4908462,5325706,22144323,24644758,5296927,7914040,13880661,22593800,275084]}}
                case "STOP_LIVE_ROOM_LIST":
                // {"cmd": "ONLINE_RANK_COUNT","data": {"count": 582}}
                case "ONLINE_RANK_COUNT":
                // {"cmd": "ENTRY_EFFECT","data": {"id": 4,"uid": 346361954,"target_id": 3821157,"mock_effect": 0,"face": "https://i2.hdslb.com/bfs/face/f4f28ca7f2b6fa166023ddd51448f02a3376f053.jpg","privilege_type": 3,"copy_writing": "欢迎舰长 <%橙帕斯%> 进入直播间","copy_color": "#ffffff","highlight_color": "#E6FF00","priority": 70,"basemap_url": "https://i0.hdslb.com/bfs/live/mlive/11a6e8eb061c3e715d0a6a2ac0ddea2faa15c15e.png","show_avatar": 1,"effective_time": 2,"web_basemap_url": "https://i0.hdslb.com/bfs/live/mlive/11a6e8eb061c3e715d0a6a2ac0ddea2faa15c15e.png","web_effective_time": 2,"web_effect_close": 0,"web_close_time": 0,"business": 1,"copy_writing_v2": "欢迎舰长 <%橙帕斯%> 进入直播间","icon_list": [],"max_delay_time": 7,"trigger_time": 1651501371289187200,"identities": 6,"effect_silent_time": 300}}
                case "ENTRY_EFFECT":
                // {"cmd": "HOT_RANK_CHANGED","data": {"rank": 40,"trend": 2,"countdown": 420,"timestamp": 1651501380,"web_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=2&area_id=9&parent_area_id=9&second_area_id=0","live_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=1&area_id=9&parent_area_id=9&second_area_id=0&is_live_half_webview=1&hybrid_rotate_d=1&hybrid_half_ui=1,3,100p,70p,f4eefa,0,30,100,12,0;2,2,375,100p,f4eefa,0,30,100,0,0;3,3,100p,70p,f4eefa,0,30,100,12,0;4,2,375,100p,f4eefa,0,30,100,0,0;5,3,100p,70p,f4eefa,0,30,100,0,0;6,3,100p,70p,f4eefa,0,30,100,0,0;7,3,100p,70p,f4eefa,0,30,100,0,0;8,3,100p,70p,f4eefa,0,30,100,0,0","blink_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=3&area_id=9&parent_area_id=9&second_area_id=0&is_live_half_webview=1&hybrid_rotate_d=1&is_cling_player=1&hybrid_half_ui=1,3,100p,70p,f4eefa,0,30,100,0,0;2,2,375,100p,f4eefa,0,30,100,0,0;3,3,100p,70p,f4eefa,0,30,100,0,0;4,2,375,100p,f4eefa,0,30,100,0,0;5,3,100p,70p,f4eefa,0,30,100,0,0;6,3,100p,70p,f4eefa,0,30,100,0,0;7,3,100p,70p,f4eefa,0,30,100,0,0;8,3,100p,70p,f4eefa,0,30,100,0,0","live_link_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=5&area_id=9&parent_area_id=9&second_area_id=0&is_live_half_webview=1&hybrid_rotate_d=1&is_cling_player=1&hybrid_half_ui=1,3,100p,70p,f4eefa,0,30,100,0,0;2,2,375,100p,f4eefa,0,30,100,0,0;3,3,100p,70p,f4eefa,0,30,100,0,0;4,2,375,100p,f4eefa,0,30,100,0,0;5,3,100p,70p,f4eefa,0,30,100,0,0;6,3,100p,70p,f4eefa,0,30,100,0,0;7,3,100p,70p,f4eefa,0,30,100,0,0;8,3,100p,70p,f4eefa,0,30,100,0,0","pc_link_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=4&is_live_half_webview=1&area_id=9&parent_area_id=9&second_area_id=0&pc_ui=338,465,f4eefa,0","icon": "https://i0.hdslb.com/bfs/live/63217712edb588864b2c714225992e7f46b0b917.png","area_name": "虚拟","rank_desc": ""}}
                case "HOT_RANK_CHANGED":
                // {"cmd": "HOT_RANK_CHANGED_V2","data": {"rank": 40,"trend": 0,"countdown": 420,"timestamp": 1651501380,"web_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=2&area_id=9&parent_area_id=9&second_area_id=371","live_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=1&area_id=9&parent_area_id=9&second_area_id=371&is_live_half_webview=1&hybrid_rotate_d=1&hybrid_half_ui=1,3,100p,70p,f4eefa,0,30,100,12,0;2,2,375,100p,f4eefa,0,30,100,0,0;3,3,100p,70p,f4eefa,0,30,100,12,0;4,2,375,100p,f4eefa,0,30,100,0,0;5,3,100p,70p,f4eefa,0,30,100,0,0;6,3,100p,70p,f4eefa,0,30,100,0,0;7,3,100p,70p,f4eefa,0,30,100,0,0;8,3,100p,70p,f4eefa,0,30,100,0,0","blink_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=3&area_id=9&parent_area_id=9&second_area_id=371&is_live_half_webview=1&hybrid_rotate_d=1&is_cling_player=1&hybrid_half_ui=1,3,100p,70p,f4eefa,0,30,100,0,0;2,2,375,100p,f4eefa,0,30,100,0,0;3,3,100p,70p,f4eefa,0,30,100,0,0;4,2,375,100p,f4eefa,0,30,100,0,0;5,3,100p,70p,f4eefa,0,30,100,0,0;6,3,100p,70p,f4eefa,0,30,100,0,0;7,3,100p,70p,f4eefa,0,30,100,0,0;8,3,100p,70p,f4eefa,0,30,100,0,0","live_link_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=5&area_id=9&parent_area_id=9&second_area_id=371&is_live_half_webview=1&hybrid_rotate_d=1&is_cling_player=1&hybrid_half_ui=1,3,100p,70p,f4eefa,0,30,100,0,0;2,2,375,100p,f4eefa,0,30,100,0,0;3,3,100p,70p,f4eefa,0,30,100,0,0;4,2,375,100p,f4eefa,0,30,100,0,0;5,3,100p,70p,f4eefa,0,30,100,0,0;6,3,100p,70p,f4eefa,0,30,100,0,0;7,3,100p,70p,f4eefa,0,30,100,0,0;8,3,100p,70p,f4eefa,0,30,100,0,0","pc_link_url": "https://live.bilibili.com/p/html/live-app-hotrank/index.html?clientType=4&is_live_half_webview=1&area_id=9&parent_area_id=9&second_area_id=371&pc_ui=338,465,f4eefa,0","icon": "https://i0.hdslb.com/bfs/live/cb2e160ac4f562b347bb5ae6e635688ebc69580f.png","area_name": "虚拟主播","rank_desc": "虚拟主播top50"}}
                case "HOT_RANK_CHANGED_V2":
                // { "cmd": "ONLINE_RANK_TOP3","data": { "dmscore": 112,"list": [{ "msg": "恭喜 <%dragonborn%> 成为高能榜","rank": 2}]} }
                case "ONLINE_RANK_TOP3":
                // {"cmd": "SUPER_CHAT_MESSAGE","data": {"background_bottom_color": "#2A60B2","background_color": "#EDF5FF","background_color_end": "#405D85","background_color_start": "#3171D2","background_icon": "","background_image": "https://i0.hdslb.com/bfs/live/a712efa5c6ebc67bafbe8352d3e74b820a00c13e.png","background_price_color": "#7497CD","color_point": 0.7,"dmscore": 120,"end_time": 1651501472,"gift": {"gift_id": 12000,"gift_name": "醒目留言","num": 1},"id": 3901718,"is_ranked": 1,"is_send_audit": 0,"medal_info": {"anchor_roomid": 21692711,"anchor_uname": "东爱璃Lovely","guard_level": 3,"icon_id": 0,"is_lighted": 1,"medal_color": "#1a544b","medal_color_border": 6809855,"medal_color_end": 5414290,"medal_color_start": 1725515,"medal_level": 23,"medal_name": "秧歌星","special": "","target_id": 3821157},"message": "多谢大璃子，我去擦一下鼻血","message_font_color": "#A3F6FF","message_trans": "","price": 30,"rate": 1000,"start_time": 1651501412,"time": 60,"token": "C7D08916","trans_mark": 0,"ts": 1651501412,"uid": 801016,"user_info": {"face": "http://i1.hdslb.com/bfs/face/9ee6482c35893220ad63e389612812327b00bdcc.jpg","face_frame": "https://i0.hdslb.com/bfs/live/80f732943cc3367029df65e267960d56736a82ee.png","guard_level": 3,"is_main_vip": 1,"is_svip": 0,"is_vip": 0,"level_color": "#61c05a","manager": 0,"name_color": "#00D1F1","title": "0","uname": "dragonborn","user_level": 16}},"roomid": 21692711}
                case "SUPER_CHAT_MESSAGE":
                // {"cmd": "COMBO_SEND","data": {"action": "投喂","batch_combo_id": "batch:gift:combo_id:277549606:3821157:31036:1651502157.7551","batch_combo_num": 10,"combo_id": "gift:combo_id:277549606:3821157:31036:1651502157.7540","combo_num": 10,"combo_total_coin": 1000,"dmscore": 120,"gift_id": 31036,"gift_name": "小花花","gift_num": 0,"is_show": 1,"medal_info": {"anchor_roomid": 0,"anchor_uname": "","guard_level": 3,"icon_id": 0,"is_lighted": 1,"medal_color": 1725515,"medal_color_border": 6809855,"medal_color_end": 5414290,"medal_color_start": 1725515,"medal_level": 22,"medal_name": "秧歌星","special": "","target_id": 3821157},"name_color": "#00D1F1","r_uname": "东爱璃Lovely","ruid": 3821157,"send_master": null,"total_num": 10,"uid": 277549606,"uname": "青言和link"}}
                case "COMBO_SEND":
                // {"cmd": "SUPER_CHAT_MESSAGE_JPN","data": {"id": "3901990","uid": "681587914","price": 30,"rate": 1000,"message": "习惯了，宝的闹，突然不闹了感觉差了什么！","message_jpn": "慣れた、宝の騒ぎ、急に騒がないと何か悪い感じがします!","is_ranked": 1,"background_image": "https://i0.hdslb.com/bfs/live/a712efa5c6ebc67bafbe8352d3e74b820a00c13e.png","background_color": "#EDF5FF","background_icon": "","background_price_color": "#7497CD","background_bottom_color": "#2A60B2","ts": 1651502270,"token": "C1BDB9D3","medal_info": {"icon_id": 0,"target_id": 3821157,"special": "","anchor_uname": "东爱璃Lovely","anchor_roomid": 21692711,"medal_level": 11,"medal_name": "秧歌星","medal_color": "#8d7ca6"},"user_info": {"uname": "懂悟","face": "http://i1.hdslb.com/bfs/face/931847b013649ba4c407b2627cbdafffc51a9e89.jpg","face_frame": "","guard_level": 0,"user_level": 11,"level_color": "#61c05a","is_vip": 0,"is_svip": 0,"is_main_vip": 1,"title": "0","manager": 0},"time": 59,"start_time": 1651502269,"end_time": 1651502329,"gift": {"num": 1,"gift_id": 12000,"gift_name": "醒目留言"}},"roomid": "21692711"}
                case "SUPER_CHAT_MESSAGE_JPN":
                // {"cmd": "COMMON_NOTICE_DANMAKU","data": {"content_segments": [{"font_color": "#FB7299","text": "劳动节限时任务：任务即将结束，抓紧完成获取185元红包奖励吧！未完成任务进度将重置","type": 1}],"dmscore": 144,"terminals": [1,2,3,4,5]}}
                case "COMMON_NOTICE_DANMAKU":
                    break;
                // {"cmd": "LIKE_INFO_V3_UPDATE","data": {"click_count": 31530}}
                case "LIKE_INFO_V3_UPDATE":
                    break;
                // {"cmd":"LIKE_INFO_V3_CLICK","data":{"show_area":0,"msg_type":6,"like_icon":"https://i0.hdslb.com/bfs/live/23678e3d90402bea6a65251b3e728044c21b1f0f.png","uid":430005513,"like_text":"为主播点赞了","uname":"Bronya我滴宝儿","uname_color":"","identities":[3,1],"fans_medal":{"target_id":11073,"medal_level":11,"medal_name":"憨毛怪","medal_color":9272486,"medal_color_start":9272486,"medal_color_end":9272486,"medal_color_border":9272486,"is_lighted":1,"guard_level":0,"special":"","icon_id":0,"anchor_roomid":0,"score":21439},"contribution_info":{"grade":0},"dmscore":20}}
                case "LIKE_INFO_V3_CLICK":
                    break;
                default:
                    Console.WriteLine($"Unknown Info Packet : {json}");
                    break;
            }
        }

        protected static async IAsyncEnumerable<Packet> ZlibDeCompressAsync(byte[] bytes)
        {
            // Skip Zlib Header
            // @see https://stackoverflow.com/questions/9050260/what-does-a-zlib-header-look-like
            // 78 01 - No Compression/low
            // 78 9C - Default Compression
            // 78 DA - Best Compression 
            await using var ms = new MemoryStream(bytes, 2, bytes.Length - 2);
            // await using var ms = new MemoryStream(bytes, 0, bytes.Length);
            await using var zs = new DeflateStream(ms, CompressionMode.Decompress);
            var len = 1;
            while (len > 0)
            {
                var headerBuffer = new byte[PacketHeader.PACKET_HEADER_LENGTH];
                len = await zs.ReadAsync(headerBuffer.AsMemory(0, PacketHeader.PACKET_HEADER_LENGTH));
                if (len == 0) break;
                var header = new PacketHeader(headerBuffer);
                var buffer = new byte[header.BodyLength];
                len = await zs.ReadAsync(buffer.AsMemory(0, buffer.Length));
                yield return new Packet { Header = header, PacketBody = buffer };
            }
        }

        protected static async IAsyncEnumerable<Packet> BrotliDecompressAsync(byte[] bytes)
        {
            await using var ms = new MemoryStream(bytes, 0, bytes.Length);
            await using var zs = new BrotliStream(ms, CompressionMode.Decompress);
            var len = 1;
            while (len > 0)
            {
                var headerBuffer = new byte[PacketHeader.PACKET_HEADER_LENGTH];
                len = await zs.ReadAsync(headerBuffer.AsMemory(0, PacketHeader.PACKET_HEADER_LENGTH));
                if (len == 0) break;
                var header = new PacketHeader(headerBuffer);
                var buffer = new byte[header.BodyLength];
                len = await zs.ReadAsync(buffer.AsMemory(0, buffer.Length));
                yield return new Packet { Header = header, PacketBody = buffer };
            }
        }

        protected async Task<JToken> GetDanmakuLinkInfoAsync(long roomID)
        {
            var resp = await _http.GetStringAsync(
                $"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={roomID}&type=0");
            return JObject.Parse(resp)["data"];
        }

        protected async Task<JToken> GetRoomInfoAsync(long roomID)
        {
            var resp = await _http.GetStringAsync(
                $"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?room_id={roomID}");
            return JObject.Parse(resp)["data"];
        }
    }



    /// <summary>
    /// 观众互动内容
    /// </summary>
    public enum InteractType
    {
        /// <summary>
        /// 进入
        /// </summary>
        Enter = 1,

        /// <summary>
        /// 关注
        /// </summary>
        Follow = 2,

        /// <summary>
        /// 分享直播间
        /// </summary>
        Share = 3,

        /// <summary>
        /// 特别关注
        /// </summary>
        SpecialFollow = 4,

        /// <summary>
        /// 互相关注
        /// </summary>
        MutualFollow = 5,
    }
}