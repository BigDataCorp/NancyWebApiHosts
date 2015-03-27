using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NancyHostLib
{
    public static class NancyCompressionExtenstion
    {
        public static void CheckForCompression (NancyContext context)
        {
            // intial sanity check
            if (context == null || context.Request == null || context.Response == null ||
                context.Response.StatusCode != HttpStatusCode.OK)
            {
                return;
            }

            // check if is compression compatible
            if (!RequestIsGzipCompatible (context))
            {
                return;
            }

            if (ContentLengthIsTooSmall (context.Response))
            {
                return;
            }

            CompressResponse (context.Response);
        }

        static void CompressResponse (Response response)
        {
            response.Headers["Content-Encoding"] = "gzip";

            var contents = response.Contents;
            response.Contents = responseStream =>
            {
                try
                {
                    using (var compression = new System.IO.Compression.GZipStream (responseStream, System.IO.Compression.CompressionLevel.Fastest))
                    {
                        contents (compression);
                    }
                }
                catch { }
            };
        }

        static bool ContentLengthIsTooSmall (Response response)
        {
            string contentLength;
            if (response.Headers.TryGetValue ("Content-Length", out contentLength))
            {
                var length = long.Parse (contentLength);
                if (length < 16 * 1024)
                {
                    return true;
                }
            }
            return false;
        }

        public static HashSet<string> ValidMimes = new HashSet<string> (StringComparer.Ordinal)
        {
            "text/css",
            "text/html",
            "text/plain",
            "application/xml",
            "application/json",
            "application/xaml+xml",
            "application/x-javascript"
        };

        static bool RequestIsGzipCompatible (NancyContext context)
        {
            return ValidMimes.Contains (context.Response.ContentType) &&
                context.Request.Headers.AcceptEncoding.Any (x => x.Contains ("gzip"));
        }
    }

}
