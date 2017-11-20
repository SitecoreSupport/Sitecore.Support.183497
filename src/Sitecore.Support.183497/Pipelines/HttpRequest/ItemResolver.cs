namespace Sitecore.Support.Pipelines.HttpRequest
{
  using Sitecore.Abstractions;
  using Sitecore.Configuration;
  using Sitecore.Data.ItemResolvers;
  using Sitecore.Data.Items;
  using Sitecore.Pipelines.HttpRequest;
  using System;
  public class ItemResolver : Sitecore.Pipelines.HttpRequest.ItemResolver
  {
    [Obsolete("Please use another constructor with parameters")]
    public ItemResolver() : base()
    {
    }

    public ItemResolver(BaseItemManager itemManager, ItemPathResolver pathResolver) : base(itemManager, pathResolver, Settings.ItemResolving.FindBestMatch)
    {
    }

    protected override Item ResolveByMixedDisplayName(HttpRequestArgs args, out bool accessDenied)
    {
      Item resolvedItem = base.ResolveByMixedDisplayName(args, out accessDenied);
      args.PermissionDenied = accessDenied;
      return resolvedItem;
    }

    protected override bool TryResolveItem(string itemPath, HttpRequestArgs args, out Item item, out bool permissionDenied)
    {
      bool tryResolveItem = base.TryResolveItem(itemPath, args, out item, out permissionDenied);
      args.PermissionDenied = permissionDenied;
      return tryResolveItem;
    }
  }
}