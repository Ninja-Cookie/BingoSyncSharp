using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace BingoSyncAPI.NetworkHandler
{
    internal static class HttpWebHandler
    {
        private const int MaxAttempts = 3;

        public static async Task<CookieContainer> TryGetCookies(HttpWebRequest request)
        {
            string cookieHeader = await GetHeader(request, HttpResponseHeader.SetCookie);

            try
            {
                if (cookieHeader != string.Empty)
                {
                    CookieContainer cookieContainer = new CookieContainer();
                    cookieContainer.SetCookies(request.Address, cookieHeader);
                    return cookieContainer;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> GetHeader(HttpWebRequest request, HttpResponseHeader header)
        {
            return await TryResponse(request, null, true, header);
        }

        public static async Task<string> TryGetResponse(string URL, bool returnResponse = true, CookieContainer cookies = null, string data = null)
        {
            HttpWebRequest request = TryGetRequest(URL);

            if (cookies != null)
                request.CookieContainer = cookies;

            DebugModeOutput.WriteLine($"Cookies: {request?.CookieContainer}");

            if (request == null)
                return string.Empty;

            if (data != null)
                await TryPost(request, data);

            DebugModeOutput.WriteLine($"Post: {data}");

            return await TryResponse(request, cookies, returnResponse);
        }

        public static HttpWebRequest TryGetRequest(string URL)
        {
            if (URL == string.Empty)
                return null;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(URL));
                return request;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> TryPost(HttpWebRequest request, string data)
        {
            if (request == null)
                return false;

            int attempt = 0;

            while (attempt < MaxAttempts)
            {
                attempt++;

                try
                {
                    request.Method      = "POST";
                    request.ContentType = "application/json; charset=UTF-8";
                    request.Accept      = "application/json";

                    using (var stream = new StreamWriter(await request.GetRequestStreamAsync()))
                    {
                        await stream.WriteAsync(data);
                        stream.Dispose();
                    }

                    return true;
                }
                catch
                {
                    if (attempt >= MaxAttempts)
                        return false;

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            return false;
        }

        public static async Task<string> TryResponse(HttpWebRequest request, CookieContainer cookies, bool returnResponse = true, HttpResponseHeader? header = null)
        {
            if (request == null)
                return string.Empty;

            int attempt = 0;

            string responseString = string.Empty;

            while (attempt < MaxAttempts)
            {
                attempt++;

                try
                {
                    using (var response = (HttpWebResponse)await request.GetResponseAsync())
                    {
                        if (!returnResponse)
                            break;
                        
                        if (header != null)
                        {
                            responseString = response.Headers[(HttpResponseHeader)header];
                            response.Dispose();
                        }
                        else
                        {
                            using (var reader = new StreamReader(response.GetResponseStream()))
                            {
                                responseString = await reader.ReadToEndAsync();
                                response.Dispose();
                                reader  .Dispose();
                            }
                        }

                        response.Close();
                    }

                    break;
                }
                catch
                {
                    DebugModeOutput.WriteLine($"Failed Response Attempt: {attempt}");
                    DebugModeOutput.WriteLine($"({request?.Address?.ToString()}) Did not like request...");

                    if (attempt >= MaxAttempts)
                        return string.Empty;

                    if (cookies != null)
                    {
                        request = TryGetRequest(request.RequestUri.AbsoluteUri);

                        if (request != null)
                            request.CookieContainer = cookies;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            return responseString ?? string.Empty;
        }
    }
}
