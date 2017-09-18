using Microsoft.Extensions.DependencyInjection;
using Sitecore.Abstractions;
using Sitecore.Configuration;
using Sitecore.Data.ItemResolvers;
using Sitecore.Data.Items;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.IO;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.SecurityModel;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sitecore.Support.Pipelines.HttpRequest
{
  public class ItemResolver : HttpRequestProcessor
  {
    public static readonly string UseSiteStartPathQueryStringKey = "sc_usesitestartpath";

    private ItemPathResolver pathResolver;

    [Obsolete("Please use another constructor with parameters")]
    public ItemResolver() : this(ServiceLocator.ServiceProvider.GetRequiredService<BaseItemManager>(), ServiceLocator.ServiceProvider.GetRequiredService<ItemPathResolver>())
    {
    }

    public ItemResolver(BaseItemManager itemManager, ItemPathResolver pathResolver) :
      this(itemManager, pathResolver, itemNameResolvingMode: Settings.ItemResolving.FindBestMatch)
    {
    }

    protected ItemResolver([NotNull] BaseItemManager itemManager, [NotNull] ItemPathResolver pathResolver, MixedItemNameResolvingMode itemNameResolvingMode)
    {
      Assert.ArgumentNotNull(itemManager, "itemManager");
      Assert.ArgumentNotNull(pathResolver, "pathResolver");

      this.ItemManager = itemManager;
      this.ItemNameResolvingMode = itemNameResolvingMode;

      var useMixedItemNameResolving = (itemNameResolvingMode & MixedItemNameResolvingMode.Enabled) == MixedItemNameResolvingMode.Enabled;

      this.pathResolver = useMixedItemNameResolving ? new MixedItemNameResolver(pathResolver) : pathResolver;
    }

    [NotNull]
    protected BaseItemManager ItemManager { get; private set; }

    protected MixedItemNameResolvingMode ItemNameResolvingMode { get; private set; }

    [NotNull]
    protected ItemPathResolver PathResolver
    {
      get
      {
        return this.pathResolver;
      }

      set
      {
        Debug.ArgumentNotNull(value, "value");

        this.pathResolver = value;
      }
    }

    public override void Process([NotNull] HttpRequestArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      var contextProxy = new Reflection.Proxy.SitecoreContextProxy(args);

      if (this.SkipItemResolving(args))
      {
        return;
      }

      bool permissionDenied = false;
      Item resolvedItem = null;

      string path = string.Empty;

      try
      {
        this.StartProfilingOperation("Resolve current item.", args);

        var uniquePaths = new HashSet<string>();
        foreach (var candidatePath in this.GetCandidatePaths(args))
        {
          if (!uniquePaths.Add(candidatePath))
          {
            continue;
          }

          if (this.TryResolveItem(candidatePath, args, out resolvedItem, out permissionDenied))
          {
            path = candidatePath;
            break;
          }

          if (permissionDenied)
          {
            args.PermissionDenied = permissionDenied;
            return;
          }
        }

        var site = contextProxy.Site;

        if (resolvedItem == null || resolvedItem.Name.Equals("*"))
        {
          var displayNameItem = this.ResolveByMixedDisplayName(args, out permissionDenied);
          if (displayNameItem != null)
          {
            resolvedItem = displayNameItem;
          }
        }

        if (resolvedItem == null && site != null && !permissionDenied && this.UseSiteStartPath(args))
        {
          if (this.TryResolveItem(site.StartPath, args, out resolvedItem, out permissionDenied))
          {
            path = site.StartPath;
          }
        }
      }
      finally
      {
        if (resolvedItem != null)
        {
          this.TraceInfo("Current item is {0}.".FormatWith(path));
        }

        contextProxy.Item = resolvedItem;

        this.EndProfilingOperation(status: null, args: args);
      }
    }

    protected virtual bool UseSiteStartPath(HttpRequestArgs args)
    {
      bool useSiteStartPath;
      bool.TryParse(this.GetQueryString(UseSiteStartPathQueryStringKey, args), out useSiteStartPath);

      return useSiteStartPath;
    }

    [NotNull]
    protected virtual IEnumerable<string> GetCandidatePaths([NotNull]HttpRequestArgs args)
    {
      var contextProxy = new Reflection.Proxy.SitecoreContextProxy(args);

      var context = contextProxy;

      var itemPath = args.Url.ItemPath;
      var decodedItemPath = this.DecodeName(itemPath);

      yield return decodedItemPath;
      yield return itemPath;

      var localPath = args.LocalPath;
      var decodedLocalPath = this.DecodeName(localPath);

      yield return localPath;
      yield return decodedLocalPath;

      var site = context.Site;
      string rootPath = site != null ? site.RootPath : string.Empty;

      var rootedLocalPath = FileUtil.MakePath(rootPath, args.LocalPath, separator: '/');

      yield return rootedLocalPath;
      yield return this.DecodeName(rootedLocalPath);

      string startItem = site != null ? site.StartItem : string.Empty;

      var rootPathArray = new[] { rootPath, this.DecodeName(rootPath) };
      var startItemArray = new[] { startItem, this.DecodeName(startItem) };
      var localPathArray = new[] { localPath, decodedLocalPath };

      var pathList = from rootPathCandidate in rootPathArray
                     from startItemCandidate in startItemArray
                     let rootItemWithStartPath = FileUtil.MakePath(rootPathCandidate, startItemCandidate, '/')
                     from localPathCandidate in localPathArray
                     let rootStartAndLocalPath = FileUtil.MakePath(rootItemWithStartPath, localPathCandidate, '/')
                     select rootStartAndLocalPath;

      foreach (var pathVariation in pathList)
      {
        yield return pathVariation;
      }
    }

    protected virtual bool TryResolveItem([NotNull] string itemPath, [NotNull] HttpRequestArgs args, out Item item, out bool permissionDenied)
    {
      var contextProxy = new Reflection.Proxy.SitecoreContextProxy(args);

      permissionDenied = false;

      var context = contextProxy;

      item = this.ItemManager.GetItem(itemPath, context.Language, Data.Version.Latest, context.Database, SecurityCheck.Disable);

      if (item == null)
      {
        return false;
      }

      if (item.Access.CanRead())
      {
        return true;
      }
      else
      {
        item = null;
        permissionDenied = true;
        return false;
      }
    }

    [NotNull]
    protected virtual string DecodeName(string itemPath)
    {
      return MainUtil.DecodeName(itemPath);
    }

    protected virtual bool SkipItemResolving([NotNull]HttpRequestArgs args)
    {
      var contextProxy = new Reflection.Proxy.SitecoreContextProxy(args);
      var context = contextProxy;
      return context.Item != null || context.Database == null || args.Url.ItemPath.Length == 0;
    }

    [CanBeNull]
    protected virtual Item ResolveEncodedMixedPath([CanBeNull]string path, [NotNull]HttpRequestArgs args)
    {
      if (string.IsNullOrEmpty(path) || path[0] != '/')
      {
        return null;
      }

      int firstItemNameEndPos = path.IndexOf('/', 1);

      if (firstItemNameEndPos < 0)
      {
        return null;
      }

      var leadingPathPart = path.Substring(0, firstItemNameEndPos);

      string remainingPart = path.Substring(firstItemNameEndPos);

      return this.ResolveEncodedMixedPath(args, leadingPathPart, remainingPart);
    }

    [CanBeNull]
    protected virtual Item ResolveEncodedMixedPath([NotNull] HttpRequestArgs args, string rootItemPath, string pathToResolveUnderRoot)
    {
      Debug.ArgumentNotNull(args, "args");

      var contextProxy = new Reflection.Proxy.SitecoreContextProxy(args);

      var context = contextProxy;

      Item rootItem = this.ItemManager.GetItem(rootItemPath, context.Language, Data.Version.Latest, context.Database, SecurityCheck.Disable);

      return rootItem == null ? null : this.PathResolver.ResolveItem(pathToResolveUnderRoot, rootItem);
    }

    [CanBeNull]
    protected virtual Item ResolveByMixedDisplayName([NotNull] HttpRequestArgs args, out bool accessDenied)
    {
      Assert.ArgumentNotNull(args, "args");

      var contextProxy = new Reflection.Proxy.SitecoreContextProxy(args);

      accessDenied = false;
      Item result = null;

      using (new SecurityDisabler())
      {
        var site = contextProxy.Site;
        if (site != null)
        {
          result = this.ResolveEncodedMixedPath(args, site.RootPath, args.LocalPath);
        }

        result = result ?? this.ResolveEncodedMixedPath(args.Url.ItemPath, args) ?? this.ResolveEncodedMixedPath(args.LocalPath, args);

        if (result == null)
        {
          return null;
        }
      }

      accessDenied = !result.Access.CanRead();

      return accessDenied ? null : result;
    }

  }
}