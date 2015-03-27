using System;
using System.Collections.Generic;
using System.Linq;
using Nancy.ModelBinding;
using Nancy;
using Newtonsoft.Json.Linq;

namespace NancyHostLib
{    
    /// <summary>
    /// based on https://gist.github.com/thecodejunkie/5521941
    /// but modified to alow binding of json content in body
    /// </summary>
    public class DynamicModelBinder : IModelBinder
    {
        public object Bind (NancyContext context, Type modelType, object instance, BindingConfig configuration, params string[] blackList)
        {
            return GetDataFields (context);
        }

        private static IDictionary<string, object> GetDataFields (NancyContext context)
        {
            return Merge (ConvertDynamicDictionary (context.Request.Query),
                ConvertDynamicDictionary (context),
                ConvertDynamicDictionary (context.Request.Form),
                ConvertDynamicDictionary (context.Parameters));
        }

        private static DynamicDictionary Merge (params IEnumerable<KeyValuePair<string, object>>[] dictionaries)
        {
            var output = new DynamicDictionary ();
            
            foreach (var dictionary in dictionaries)
            {
                if (dictionary == null)
                    continue;
                foreach (var kvp in dictionary)
                {
                    if (!output.ContainsKey (kvp.Key))
                    {
                        output.Add (kvp.Key, kvp.Value);
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
            return modelType == typeof (DynamicDictionary);
        }
    }
}