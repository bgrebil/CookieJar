using System;
using System.Collections.Specialized;
using System.Linq;

namespace CookieJar
{
   internal static class Extensions
   {
      public static string ReadValue(this NameValueCollection collection, string key, string defaultValue)
      {
         if (String.IsNullOrWhiteSpace(collection[key])) {
            collection.Remove(key);
            collection.Add(key, defaultValue);
         }
         return collection[key];
      }

      public static bool ReadBool(this NameValueCollection collection, string key, bool defaultValue)
      {
         return Convert.ToBoolean(collection.ReadValue(key, defaultValue.ToString()));
      }
   }
}
