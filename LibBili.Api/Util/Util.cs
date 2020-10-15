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
            StringBuilder sb = new StringBuilder();
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
    }
}
