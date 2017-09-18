using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Globalization;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.Security.Accounts;
using Sitecore.Sites;
using System;
using System.Collections;
using System.Reflection;

namespace Sitecore.Support.Reflection.Proxy
{
  public class SitecoreContextProxy
  {
    private static readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private object _context;

    private PropertyInfo GetProperty(string name, object obj = null)
    {
      Type type = (obj == null) ? _context.GetType() : obj.GetType();
      PropertyInfo prop = null;
      do
      {
        prop = type.GetProperty(name, bFlags);
        type = (prop == null) ? type.BaseType : type;
      }
      while (prop == null);

      return prop;
    }

    public SitecoreContextProxy(HttpRequestArgs args)
    {
      this._context = GetProperty("SitecoreContext", args).GetValue(args);
    }

    public Context.ContextData Data
    {
      get
      {
        return GetProperty("Data").GetValue(_context) as Context.ContextData;
      }
    }

    public Database Database
    {
      get
      {
        return GetProperty("Database").GetValue(_context) as Database;
      }
      set
      {
        GetProperty("Database").SetValue(_context, value);
      }
    }

    public Item Item
    {
      get
      {
        return GetProperty("Item").GetValue(_context) as Item;
      }
      set
      {
        GetProperty("Item").SetValue(_context, value);
      }
    }

    public IDictionary Items
    {
      get
      {
        return GetProperty("Items").GetValue(_context) as IDictionary;
      }
    }

    public Language Language
    {
      get
      {
        return GetProperty("Language").GetValue(_context) as Language;
      }
      set
      {
        GetProperty("Language").SetValue(_context, value);
      }
    }

    public SiteContext Site
    {
      get
      {
        return GetProperty("Site").GetValue(_context) as SiteContext;
      }
      set
      {
        GetProperty("Site").SetValue(_context, value);
      }
    }

    public User User
    {
      get
      {
        return GetProperty("User").GetValue(_context) as User;
      }
    }
  }

}
