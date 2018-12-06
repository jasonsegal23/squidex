﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschränkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

#pragma warning disable SA1401 // Fields must be private

namespace Squidex.Infrastructure.Caching
{
    internal class LRUCacheItem<TKey, TValue>
    {
        public TKey Key;

        public TValue Value;
    }
}