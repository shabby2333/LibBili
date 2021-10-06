using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace LibBili.Api.Util
{
    public static class Util
    {
        public static IDictionary<string, string> ToDictionary(this CookieCollection cookies)
        {
            var cks = new Dictionary<string, string>();
            foreach(Cookie cookie in cookies)
                cks.Add(cookie.Name, cookie.Value);
            return cks;
        }

        public static string ToCookieString(this CookieCollection cookies)
        {
            var sb = new StringBuilder();
            foreach (Cookie cookie in cookies)
                sb.Append($"{cookie.Name}={cookie.Value}; ");
            return sb.ToString();
        }

        public static CookieCollection ToCookieCollection(IEnumerable<KeyValuePair<string, string>> cks)
        {
            var cookies = new CookieCollection();
            foreach(var e in cks)
                cookies.Add(new Cookie(e.Key, e.Value));
            return cookies;
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="event">事件的EventHandler对象</param>
        /// <param name="sender">发送者</param>
        /// <param name="e">传递的数据</param>
        public static void Emit(this EventHandler @event, object sender, EventArgs e) => @event(sender, e);
        /// <summary>
        /// 触发事件（泛型）
        /// </summary>
        /// <param name="event">事件的EventHandler<T>对象</param>
        /// <param name="sender">发送者</param>
        /// <param name="e">传递的数据</param>
        public static void Emit<T>(this EventHandler<T> @event, object sender, T e) => @event(sender, e);
        
    
    }
}
