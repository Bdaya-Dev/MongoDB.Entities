﻿using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#nullable enable
namespace MongoDB.Entities
{
    /// <summary>
    /// MongoContext is a wrapper around an <see cref="IMongoClient"/>
    /// </summary>
    public partial class MongoContext : IMongoClient
    {
        /// <summary>
        /// Creates a new context
        /// </summary>        
        /// <param name="client">The backing client, usually a <see cref="MongoClient"/></param>
        /// <param name="options">The options to configure the context</param>
        public MongoContext(IMongoClient client, MongoContextOptions? options = null)
        {
            Client = client;
            Options = options ?? new();
        }

        /// <summary>
        /// The backing client
        /// </summary>
        public IMongoClient Client { get; }

        public MongoContextOptions Options { get; set; }

        /// <inheritdoc cref="MongoContextOptions.ModifiedBy"/>
        public ModifiedBy? ModifiedBy => Options.ModifiedBy;

        public async Task<List<string>> AllDatabaseNamesAsync()
        {
            return await (await
                   ((IMongoClient)this)
                   .ListDatabaseNamesAsync().ConfigureAwait(false))
                   .ToListAsync().ConfigureAwait(false);
        }

        private Type[]? _allEntitiyTypes;
        public Type[] AllEntitiyTypes => _allEntitiyTypes ??= GetAllEntityTypes();

        private readonly ConcurrentDictionary<Type, Cache> _cache = new();
        internal Cache<T> Cache<T>() where T : IEntity
        {
            if (!_cache.TryGetValue(typeof(T), out var c))
            {
                c = new Cache<T>();
            }
            return (Cache<T>)c;
        }

        private static Type[] GetAllEntityTypes()
        {
            var excludes = new[]
                {
                    "Microsoft.",
                    "System.",
                    "MongoDB.",
                    "testhost.",
                    "netstandard",
                    "Newtonsoft.",
                    "mscorlib",
                    "NuGet."
                };

            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a =>
                      !a.IsDynamic &&
                      (a.FullName.StartsWith("MongoDB.Entities.Tests") || !excludes.Any(n => a.FullName.StartsWith(n))))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IEntity).IsAssignableFrom(t))
                .ToArray();
        }

    }

}
