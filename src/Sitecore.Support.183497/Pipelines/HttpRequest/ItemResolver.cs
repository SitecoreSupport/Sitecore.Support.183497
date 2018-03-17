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
    public class ItemResolver : Sitecore.Pipelines.HttpRequest.ItemResolver
    {
        [Obsolete("Please use another constructor with parameters")]
        public ItemResolver()
        : this(ServiceLocator.ServiceProvider.GetRequiredService<BaseItemManager>(), ServiceLocator.ServiceProvider.GetRequiredService<ItemPathResolver>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Sitecore.Pipelines.HttpRequest.ItemResolver" /> class.
        /// </summary>
        /// <param name="itemManager">The item manager.</param>
        /// <param name="pathResolver">The path resolver.</param>
        public ItemResolver(BaseItemManager itemManager, ItemPathResolver pathResolver) : base(itemManager, pathResolver)

        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Sitecore.Pipelines.HttpRequest.ItemResolver" /> class.
        /// </summary>
        /// <param name="itemManager">The item manager.</param>
        /// <param name="pathResolver">The path resolver.</param>
        /// <param name="itemNameResolvingMode">The item name resolving mode.</param>
        protected ItemResolver(BaseItemManager itemManager, ItemPathResolver pathResolver, MixedItemNameResolvingMode itemNameResolvingMode) : base(itemManager, pathResolver, itemNameResolvingMode)
        {
        }

        public override void Process(HttpRequestArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

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
                        continue; // Already checked this URL
                    }

                    if (this.TryResolveItem(candidatePath, args, out resolvedItem, out permissionDenied))
                    {
                        path = candidatePath; // found matching item by path, will stop search
                        break;
                    }

                    if (permissionDenied)
                    {
                        return; // found item exists, but we cannot touch it due to lack of permissions.
                    }
                }

                var site = Sitecore.Context.Site;

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

                args.PermissionDenied = permissionDenied;
                Sitecore.Context.Item = resolvedItem;

                this.EndProfilingOperation(status: null, args: args);
            }

        }
    }

}