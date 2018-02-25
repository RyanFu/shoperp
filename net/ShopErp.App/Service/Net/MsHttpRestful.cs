﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace ShopErp.App.Service.Net
{
    public class MsHttpRestful
    {
        private static readonly JsonSerializerSettings JsonDatetimeSetting = new JsonSerializerSettings { DateFormatHandling = DateFormatHandling.MicrosoftDateFormat };
        private static readonly Regex RegUrlEncoding = new Regex(@"%[a-f0-9]{2}");
        private static readonly Dictionary<string, string> EmptyDicValues = new Dictionary<string, string>();

        public static string UrlEncode(string str, Encoding e)
        {
            if (str == null)
            {
                return "";
            }
            String stringToEncode = HttpUtility.UrlEncode(str, e ?? Encoding.UTF8).Replace("+", "%20").Replace("*", "%2A").Replace("(", "%28").Replace(")", "%29");
            return RegUrlEncoding.Replace(stringToEncode, m => m.Value.ToUpperInvariant());
        }

        private static System.Net.Http.HttpClient SetupClient(IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            int timeout = Debugger.IsAttached ? 60 : 1;
            var client = new System.Net.Http.HttpClient { Timeout = new TimeSpan(0, timeout, 0) };
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd(accept ?? "");
            client.DefaultRequestHeaders.Referrer = referrer == null ? null : new Uri(referrer);
            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
                }
            }
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36");
            return client;
        }

        private static Exception RaiseSourceException(Exception ex)
        {
            Exception e = ex;
            while (e.InnerException != null)
            {
                e = e.InnerException;
            }
            return e;
        }


        #region 返回字符的方法

        public static string PostJsonBodyReturnString(string url, IDictionary<string, object> values, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var data = PostJsonBodyReturnBytes(url, values, encoding, headers, referrer, accept);
            if (encoding != null)
            {
                return encoding.GetString(data);
            }
            return (encoding ?? Encoding.UTF8).GetString(data);
        }

        public static String PostBytesBodyReturnString(string url, byte[] body, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var data = PostBytesBodyReturnBytes(url, body, encoding, headers, referrer, accept);
            if (encoding != null)
            {
                return encoding.GetString(data);
            }
            return (encoding ?? Encoding.UTF8).GetString(data);
        }

        public static string PostUrlEncodeBodyReturnString(string url, IDictionary<string, string> values, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var data = PostUrlEncodeBodyReturnBytes(url, values, encoding, headers, referrer, accept);
            if (encoding != null)
            {
                return encoding.GetString(data);
            }
            return (encoding ?? Encoding.UTF8).GetString(data);
        }

        public static string PostMultipartFormDataBodyReturnString(string url, IDictionary<string, object> values, IList<string> fileNames, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var data = PostMultipartFormDataBodyReturnBytes(url, values, fileNames, encoding, headers, referrer, accept);
            return (encoding ?? Encoding.UTF8).GetString(data);
        }

        public static string GetUrlEncodeBodyReturnString(string url, IDictionary<string, string> values, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var data = GetUrlEncodeBodyReturnBytes(url, values, encoding, headers, referrer, accept);
            return (encoding ?? Encoding.UTF8).GetString(data);
        }

        #endregion

        #region 返回字节数据

        public static byte[] PostJsonBodyReturnBytes(string url, IDictionary<string, object> values, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var client = SetupClient(headers, referrer, accept);
            var json = values == null || values.Count < 1 ? "" : Newtonsoft.Json.JsonConvert.SerializeObject(values, JsonDatetimeSetting);
            var content = new System.Net.Http.StringContent(json ?? "", encoding ?? Encoding.UTF8);
            content.Headers.ContentType.CharSet = (encoding ?? Encoding.UTF8).BodyName;
            content.Headers.ContentType.MediaType = "application/json";
            HttpResponseMessage ret = null;
            try
            {
                ret = client.PostAsync(url, content).Result;
            }
            catch (Exception ex)
            {
                throw RaiseSourceException(ex);
            }
            if (ret.IsSuccessStatusCode == false)
            {
                throw new Exception("HTTP请求错误:" + ret.StatusCode);
            }
            var data = ret.Content.ReadAsByteArrayAsync().Result;
            return data;
        }

        public static byte[] PostBytesBodyReturnBytes(string url, byte[] body, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var client = SetupClient(headers, referrer, accept);

            HttpResponseMessage ret = null;
            try
            {
                ret = client.PostAsync(url, new System.Net.Http.ByteArrayContent(body ?? new byte[0])).Result;
            }
            catch (Exception ex)
            {
                throw RaiseSourceException(ex);
            }
            if (ret.IsSuccessStatusCode == false)
            {
                throw new Exception("HTTP请求错误:" + ret.StatusCode);
            }
            var data = ret.Content.ReadAsByteArrayAsync().Result;
            return data;
        }

        public static byte[] PostUrlEncodeBodyReturnBytes(string url, IDictionary<string, string> values, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var client = SetupClient(headers, referrer, accept);
            var content = new System.Net.Http.FormUrlEncodedContent(values ?? EmptyDicValues);
            content.Headers.ContentType.CharSet = (encoding ?? Encoding.UTF8).BodyName;
            content.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";

            HttpResponseMessage ret = null;
            try
            {
                ret = client.PostAsync(url, content).Result;
            }
            catch (Exception ex)
            {
                throw RaiseSourceException(ex);
            }
            if (ret.IsSuccessStatusCode == false)
            {
                throw new Exception("HTTP请求错误:" + ret.StatusCode);
            }
            var data = ret.Content.ReadAsByteArrayAsync().Result;
            return data;
        }

        public static byte[] PostMultipartFormDataBodyReturnBytes(string url, IDictionary<string, object> values, IList<string> fileNames, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            var content = new System.Net.Http.MultipartFormDataContent();
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            int i = 0;
            foreach (var item in values)
            {
                if (item.Value == null)
                {
                    content.Add(new System.Net.Http.StringContent(""), item.Key);
                }
                else if (item.Value.GetType() == typeof(byte[]))
                {
                    var cc = new System.Net.Http.StreamContent(new MemoryStream((byte[])item.Value));
                    cc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    content.Add(cc, item.Key, fileNames[i]);
                    i++;
                }
                else if (item.Value is DateTime)
                {
                    content.Add(new System.Net.Http.StringContent(((DateTime)item.Value).ToString("yyyy-MM-dd HH:mm:ss")), item.Key);
                }
                else if (item.Value is Enum)
                {
                    content.Add(new System.Net.Http.StringContent((Convert.ToInt32(item.Value)).ToString()), item.Key);
                }
                else if (item.Value is double)
                {
                    content.Add(new System.Net.Http.StringContent(((double)item.Value).ToString("F2")), item.Key);
                }
                else if (item.Value is float)
                {
                    content.Add(new System.Net.Http.StringContent(((float)item.Value).ToString("F2")), item.Key);
                }
                else
                {
                    content.Add(new System.Net.Http.StringContent(item.Value.ToString()), item.Key);
                }
            }
            var client = SetupClient(headers, referrer, accept);
            HttpResponseMessage ret = null;
            try
            {
                ret = client.PostAsync(url, content).Result;
            }
            catch (Exception ex)
            {
                throw RaiseSourceException(ex);
            }
            if (ret.IsSuccessStatusCode == false)
            {
                throw new Exception("HTTP请求错误:" + ret.StatusCode);
            }
            var data = ret.Content.ReadAsByteArrayAsync().Result;
            return data;
        }

        public static byte[] GetUrlEncodeBodyReturnBytes(string url, IDictionary<string, string> values, Encoding encoding = null, IDictionary<string, string> headers = null, string referrer = null, string accept = null)
        {
            if (values != null && values.Count > 0)
            {
                url += "?" + string.Join("&", values.Select(obj => obj.Key + "=" + UrlEncode(obj.Value, encoding ?? Encoding.UTF8)));
            }
            var client = SetupClient(headers, referrer, accept);

            HttpResponseMessage ret = null;
            try
            {
                ret = client.GetAsync(url).Result;
            }
            catch (Exception ex)
            {
                throw RaiseSourceException(ex);
            }
            if (ret.IsSuccessStatusCode == false)
            {
                throw new Exception("HTTP请求错误:" + ret.StatusCode);
            }
            var data = ret.Content.ReadAsByteArrayAsync().Result;
            return data;
        }

        #endregion

        public static T DoWithRetry<T>(Func<T> func, int retryCount = 3)
        {
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
                    if (i == retryCount - 1)
                    {
                        throw ex;
                    }
                    System.Threading.Thread.Sleep((i + 1) * 1500);
                }
            }
            throw new Exception("代码应该永远执行不到这里");
        }
    }
}