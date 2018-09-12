﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Squidex.Domain.Apps.Core.Tags;
using Squidex.Domain.Apps.Entities.Assets.Repositories;
using Squidex.Domain.Apps.Entities.Assets.State;
using Squidex.Domain.Apps.Entities.Backup;
using Squidex.Domain.Apps.Events.Assets;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Assets;
using Squidex.Infrastructure.EventSourcing;
using Squidex.Infrastructure.States;
using Squidex.Infrastructure.Tasks;

namespace Squidex.Domain.Apps.Entities.Assets
{
    public sealed class BackupAssets : BackupHandlerWithStore
    {
        private const string TagsFile = "AssetTags.json";
        private readonly HashSet<Guid> assetIds = new HashSet<Guid>();
        private readonly IAssetStore assetStore;
        private readonly IAssetRepository assetRepository;
        private readonly ITagService tagService;

        public override string Name { get; } = "Assets";

        public BackupAssets(IStore<Guid> store,
            IAssetStore assetStore,
            IAssetRepository assetRepository,
            ITagService tagService)
            : base(store)
        {
            Guard.NotNull(assetStore, nameof(assetStore));
            Guard.NotNull(assetRepository, nameof(assetRepository));
            Guard.NotNull(tagService, nameof(tagService));

            this.assetStore = assetStore;
            this.assetRepository = assetRepository;
            this.tagService = tagService;
        }

        public override Task BackupAsync(Guid appId, BackupWriter writer)
        {
            return BackupTagsAsync(appId, writer);
        }

        public override Task BackupEventAsync(Envelope<IEvent> @event, Guid appId, BackupWriter writer)
        {
            switch (@event.Payload)
            {
                case AssetCreated assetCreated:
                    return WriteAssetAsync(assetCreated.AssetId, assetCreated.FileVersion, writer);
                case AssetUpdated assetUpdated:
                    return WriteAssetAsync(assetUpdated.AssetId, assetUpdated.FileVersion, writer);
            }

            return TaskHelper.Done;
        }

        public override Task RestoreEventAsync(Envelope<IEvent> @event, Guid appId, BackupReader reader, RefToken actor)
        {
            switch (@event.Payload)
            {
                case AssetCreated assetCreated:
                    return ReadAssetAsync(assetCreated.AssetId, assetCreated.FileVersion, reader);
                case AssetUpdated assetUpdated:
                    return ReadAssetAsync(assetUpdated.AssetId, assetUpdated.FileVersion, reader);
            }

            return TaskHelper.Done;
        }

        public override async Task RestoreAsync(Guid appId, BackupReader reader)
        {
            await RestoreTagsAsync(appId, reader);

            await RebuildManyAsync(assetIds, id => RebuildAsync<AssetState, AssetGrain>(id, (e, s) => s.Apply(e)));
        }

        private async Task RestoreTagsAsync(Guid appId, BackupReader reader)
        {
            var tags = await reader.ReadJsonAttachmentAsync(TagsFile);

            await tagService.RebuildTagsAsync(appId, TagGroups.Assets, tags.ToObject<TagSet>());
        }

        private async Task BackupTagsAsync(Guid appId, BackupWriter writer)
        {
            var tags = await tagService.GetExportableTagsAsync(appId, TagGroups.Assets);

            await writer.WriteJsonAsync(TagsFile, JObject.FromObject(tags));
        }

        private Task WriteAssetAsync(Guid assetId, long fileVersion, BackupWriter writer)
        {
            return writer.WriteBlobAsync(GetName(assetId, fileVersion), stream =>
            {
                return assetStore.DownloadAsync(assetId.ToString(), fileVersion, null, stream);
            });
        }

        private Task ReadAssetAsync(Guid assetId, long fileVersion, BackupReader reader)
        {
            assetIds.Add(assetId);

            return reader.ReadBlobAsync(GetName(reader.OldGuid(assetId), fileVersion), async stream =>
            {
                try
                {
                    await assetStore.UploadAsync(assetId.ToString(), fileVersion, null, stream);
                }
                catch (AssetAlreadyExistsException)
                {
                    return;
                }
            });
        }

        private static string GetName(Guid assetId, long fileVersion)
        {
            return $"{assetId}_{fileVersion}.asset";
        }
    }
}
