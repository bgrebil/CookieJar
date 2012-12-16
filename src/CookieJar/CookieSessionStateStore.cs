using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Security;
using System.Web.SessionState;

namespace CookieJar
{
   // There is no shared state for session data across requests since cookies are sent with every request.
   // This means that each request is exclusive by default and there is no reason to treat this any different.
   // This also means that parallel requests may not work as you would expect in regards to session state as
   // the last response sent and received by client will have the next valid session data, regardless of 
   // changes that might have been made during the other requests.
   public sealed class CookieSessionStateStore : SessionStateStoreProviderBase
   {
      private SessionStateSection _config = null;
      private string _cookieName = ".DTSTR";
      private bool _httpOnly = true;
      private bool _secureOnly = false;
      private bool _setExpiration = false;

      public override void Initialize(string name, NameValueCollection config)
      {
         if (config == null) {
            throw new ArgumentException("config");
         }

         if (String.IsNullOrWhiteSpace(name)) {
            name = "CookieSessionStateStore";
         }

         if (String.IsNullOrWhiteSpace(config["description"])) {
            config.Remove("description");
            config.Add("description", "Cookie session state store provider");
         }

         _cookieName = config.ReadValue("cookieName", _cookieName);
         _httpOnly = config.ReadBool("httpOnly", _httpOnly);
         _secureOnly = config.ReadBool("secureOnly", _secureOnly);
         _setExpiration = config.ReadBool("setExpiration", _setExpiration);

         base.Initialize(name, config);

         Configuration cfg = WebConfigurationManager.OpenWebConfiguration(HostingEnvironment.ApplicationVirtualPath);
         _config = (SessionStateSection)cfg.GetSection("system.web/sessionState");
      }

      public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
      {
         return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
      }

      public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
      {
         // This method is used with cookieless session, which you are obviously not using if you
         // are storing your session data in a cookie.
      }

      public override void Dispose()
      {
         
      }

      public override void EndRequest(HttpContext context)
      {
         
      }

      public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
      {
         return GetSessionStoreItem(context, id, out locked, out lockAge, out lockId, out actions);
      }

      public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
      {
         return GetSessionStoreItem(context, id, out locked, out lockAge, out lockId, out actions);
      }

      public override void InitializeRequest(HttpContext context)
      {
         
      }

      public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
      {
         // Since we don't have the concept of exclusive with cookies, there is nothing to really release
      }

      public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
      {
         context.Response.Cookies.Remove(_cookieName);
      }

      public override void ResetItemTimeout(HttpContext context, string id)
      {
         
      }

      public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
      {
         // We are adding the generated session id to the session data being serialized and sent.
         // See the comment in GetSessionStoreItem for why we are doing this.
         item.Items["CookieJar_" + _config.CookieName] = id;
         string sessionItems = Serialize((SessionStateItemCollection)item.Items);

         context.Response.Cookies.Remove(_cookieName);
         if (!_secureOnly || context.Request.IsSecureConnection) {
            context.Response.Cookies.Add(CreateCookie(sessionItems));
         }
      }

      public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
      {
         return false;
      }

      private HttpCookie CreateCookie(string value)
      {
         var cookie = new HttpCookie(_cookieName, value) {
            HttpOnly = _httpOnly,
            Secure = _secureOnly
         };

         if (_setExpiration) {
            cookie.Expires = DateTime.UtcNow.Add(_config.Timeout);
         }

         return cookie;
      }

      private SessionStateStoreData GetSessionStoreItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
      {
         SessionStateStoreData item = null;
         
         var cookie = context.Request.Cookies[_cookieName];
         if (cookie != null) {
            item = Deserialize(context, cookie.Value, _config.Timeout.Minutes);

            if (item != null) {
               // Pull the session id value we stored with the session data and compare it against the 
               // current session id token. If the values are different, clear out all session data;
               // otherwise remove just the session id value from the session so it is not seen
               // further on in the request.
               var cookieIdValue = item.Items["CookieJar_" + _config.CookieName];
               if (cookieIdValue == null || cookieIdValue.ToString() != id) {
                  item.Items.Clear();
               }
               else {
                  item.Items.Remove("CookieJar_" + _config.CookieName);
               }
            }
         }

         // need to set the out parameters
         lockAge = TimeSpan.Zero;
         lockId = null;
         locked = false;
         actions = SessionStateActions.None;

         return item;
      }

      private string Serialize(SessionStateItemCollection items)
      {
         using (MemoryStream ms = new MemoryStream()) {
            using (BinaryWriter writer = new BinaryWriter(ms)) {
               if (items != null) {
                  items.Serialize(writer);
               }
            }

            var encryptedData = MachineKey.Protect(ms.ToArray(), "Session Data");
            return Convert.ToBase64String(encryptedData);
         }
      }

      private SessionStateStoreData Deserialize(HttpContext context, string serializedItems, int timeout)
      {
         try {
            var encryptedBytes = Convert.FromBase64String(serializedItems);
            var decryptedBytes = MachineKey.Unprotect(encryptedBytes, "Session Data");

            SessionStateItemCollection sessionItems = new SessionStateItemCollection();
            if (decryptedBytes != null) {
               MemoryStream ms = new MemoryStream(decryptedBytes);

               if (ms.Length > 0) {
                  BinaryReader reader = new BinaryReader(ms);
                  sessionItems = SessionStateItemCollection.Deserialize(reader);
               }
            }

            return new SessionStateStoreData(sessionItems, SessionStateUtility.GetSessionStaticObjects(context), timeout);
         }
         catch {
            return null;
         }
      }
   }
}
