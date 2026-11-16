using System;
using System.IO;
using System.Net;
using System.Threading;

namespace YgoMasterClient
{
    static class SimpleProxy
    {
        public static void Run()
        {
            if (ClientSettings.ProxyPort == 0)
            {
                Console.WriteLine("Can't create proxy server on port " + ClientSettings.ProxyPort);
                return;
            }

            new Thread(delegate ()
            {
                try
                {
                    HttpListener listener = new HttpListener();
                    listener.Prefixes.Add("http://localhost:" + ClientSettings.ProxyPort + "/");
                    listener.Start();
                    while (true)
                    {
                        try
                        {
                            HttpListenerContext context = listener.GetContext();
                            try
                            {
                                Process(context);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("[Proxy] error " + e);
                            }
                            finally
                            {
                                context.Response.Close();
                            }
                        }
                        catch
                        {
                        }
                        Thread.Sleep(100);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("[ERROR] Failed to start proxy server on port " + ClientSettings.ProxyPort + ". Error: " + e);
                }
            }).Start();
        }

        static void Process(HttpListenerContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            long contentLength = context.Request.ContentLength64;
            // ContentLength64 is -1 for chunked requests and could be huge for
            // malformed requests. Both would throw when creating the buffer.
            if (contentLength < 0 || contentLength > 16 * 1024 * 1024)
            {
                Console.WriteLine("[Proxy] Unsupported request body length: " + contentLength);
                return;
            }

            byte[] requestBuffer = new byte[(int)contentLength];
            int readBytes = 0;
            int offset = 0;
            while ((readBytes = context.Request.InputStream.Read(requestBuffer, offset, requestBuffer.Length - offset)) > 0)
            {
                offset += readBytes;
            }
            if (offset != requestBuffer.Length)
            {
                Console.WriteLine("[WARNING] Read " + offset + " / " + context.Request.ContentLength64 + " bytes");
                return;
            }

            using (WebClient webClient = new WebClient())
            {
                webClient.Proxy = null;
                foreach (string key in context.Request.Headers.Keys)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "atoken":
                        case "x-unity-version":
                        case "x_acts":
                            webClient.Headers[key] = context.Request.Headers[key];
                            continue;
                    }
                }
                byte[] responseBuffer = webClient.UploadData("http://" + ClientSettings.BaseIP + ":" + ClientSettings.BasePort + context.Request.RawUrl, requestBuffer);

                context.Response.KeepAlive = false;
                context.Response.Headers[HttpResponseHeader.ContentType] = "application/octet-stream";
                context.Response.ContentLength64 = responseBuffer.Length;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
            }
        }
    }
}