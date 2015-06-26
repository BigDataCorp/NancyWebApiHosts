using System;
using System.Collections.Generic;
using System.Linq;
using Nancy.ModelBinding;
using Nancy;
using NancyApiHost.Interfaces.SimpleHelpers;

namespace NancyApiHost
{    
    /// <summary>
    /// based on https://gist.github.com/thecodejunkie/5521941
    /// but modified to alow binding of json content in body
    /// </summary>
    public class FlexibleObjectBinder : IModelBinder
    {
        public object Bind (NancyContext context, Type modelType, object instance, BindingConfig configuration, params string[] blackList)
        {
            return Merge (ConvertDynamicDictionary (context.Request.Query),
                ConvertDynamicDictionary (context),
                ConvertDynamicDictionary (context.Request.Form),
                ConvertDynamicDictionary (context.Parameters));
        }

        private static FlexibleObject Merge (params IEnumerable<KeyValuePair<string, object>>[] dictionaries)
        {
            var output = new FlexibleObject (true);
            
            foreach (var dictionary in dictionaries)
            {
                if (dictionary == null)
                    continue;
                foreach (var kvp in dictionary)
                {
                    if (!output.Data.ContainsKey (kvp.Key))
                    {
                        output.Data.Add (kvp.Key, kvp.Value.ToString ());
                    }
                }
            }

            return output;
        }

        private static IEnumerable<KeyValuePair<string, object>> ConvertDynamicDictionary (DynamicDictionary dictionary)
        {
            return dictionary.GetDynamicMemberNames ().Select (i => new KeyValuePair<string, object> (i, dictionary[i]));
        }

        private static IEnumerable<KeyValuePair<string, object>> ConvertDynamicDictionary (NancyContext context)
        {
            var t = context.Request.Headers.ContentType;
            if (t == null || t.IndexOf ("json", StringComparison.OrdinalIgnoreCase) < 0)
                return null;
            if (context.Request.Body != null)
            {
                if (context.Request.Body.CanSeek)
                    context.Request.Body.Seek (0, System.IO.SeekOrigin.Begin);
                using (var te = new System.IO.StreamReader (context.Request.Body))
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>> (te.ReadToEnd ());
            }
            return null;
        }

        public bool CanBind (Type modelType)
        {
            return modelType == typeof (FlexibleObject);
        }
    }
}