﻿using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MongoDB.Entities
{
    internal static class Cache<T> where T : IEntity
    {
        internal static IMongoDatabase Database { get; private set; }
        internal static IMongoCollection<T> Collection { get; private set; }
        internal static string DBName { get; private set; }
        internal static string CollectionName { get; private set; }
        internal static ConcurrentDictionary<string, Watcher<T>> Watchers { get; private set; }
        internal static bool HasCreatedOn { get; private set; }
        internal static bool HasModifiedOn { get; private set; }
        internal static string ModifiedOnPropName { get; private set; }
        internal static PropertyInfo ModifiedByProp { get; private set; }
        internal static bool HasIgnoreIfDefaultProps { get; private set; }

        private static PropertyInfo[] updatableProps;
        private static ProjectionDefinition<T> requiredPropsProjection;

        static Cache()
        {
            Initialize();
            DB.DefaultDbChanged += Initialize;
        }

        private static void Initialize()
        {
            var type = typeof(T);

            Database = TypeMap.GetDatabase(type);
            DBName = Database.DatabaseNamespace.DatabaseName;

            var collAttrb = type.GetCustomAttribute<CollectionAttribute>(false) ??
#pragma warning disable CS0618 // Type or member is obsolete
                type.GetCustomAttribute<NameAttribute>(false);
#pragma warning restore CS0618 // Type or member is obsolete

            CollectionName = collAttrb != null ? collAttrb.Name : type.Name;

            if (string.IsNullOrWhiteSpace(CollectionName) || CollectionName.Contains("~"))
                throw new ArgumentException($"{CollectionName} is an illegal name for a collection!");

            Collection = Database.GetCollection<T>(CollectionName);
            TypeMap.AddCollectionMapping(type, CollectionName);

            Watchers = new ConcurrentDictionary<string, Watcher<T>>();

            var interfaces = type.GetInterfaces();
            HasCreatedOn = interfaces.Any(i => i == typeof(ICreatedOn));
            HasModifiedOn = interfaces.Any(i => i == typeof(IModifiedOn));
            ModifiedOnPropName = nameof(IModifiedOn.ModifiedOn);

            updatableProps = type.GetProperties()
                .Where(p =>
                       p.PropertyType.Name != ManyBase.PropTypeName &&
                      !p.IsDefined(typeof(BsonIdAttribute), false) &&
                      !p.IsDefined(typeof(BsonIgnoreAttribute), false))
                .ToArray();

            HasIgnoreIfDefaultProps = updatableProps.Any(p =>
                    p.IsDefined(typeof(BsonIgnoreIfDefaultAttribute), false) ||
                    p.IsDefined(typeof(BsonIgnoreIfNullAttribute), false));

            try
            {
                ModifiedByProp = updatableProps.SingleOrDefault(p =>
                                p.PropertyType == typeof(ModifiedBy) ||
                                p.PropertyType.IsSubclassOf(typeof(ModifiedBy)));
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("Multiple [ModifiedBy] properties are not allowed on entities!");
            }
        }

        internal static IEnumerable<PropertyInfo> UpdatableProps(T entity)
        {
            if (HasIgnoreIfDefaultProps)
            {
                return updatableProps.Where(p =>
                    !(p.IsDefined(typeof(BsonIgnoreIfDefaultAttribute), false) && p.GetValue(entity) == default) &&
                    !(p.IsDefined(typeof(BsonIgnoreIfNullAttribute), false) && p.GetValue(entity) == null));
            }
            return updatableProps;
        }

        internal static ProjectionDefinition<T, TProjection> CombineWithRequiredProps<TProjection>(ProjectionDefinition<T, TProjection> userProjection)
        {
            if (userProjection == null)
                throw new InvalidOperationException("Please use .Project() method before .IncludeRequiredProps()");

            if (requiredPropsProjection is null)
            {
                requiredPropsProjection = "{_id:1}";

                var props = typeof(T)
                    .GetProperties()
                    .Where(p => p.IsDefined(typeof(BsonRequiredAttribute), false));

                if (!props.Any())
                    throw new InvalidOperationException("Unable to find any entity properties marked with [BsonRequired] attribute!");

                FieldAttribute attr;
                foreach (var p in props)
                {
                    attr = p.GetCustomAttribute<FieldAttribute>();

                    if (attr is null)
                        requiredPropsProjection = requiredPropsProjection.Include(p.Name);
                    else
                        requiredPropsProjection = requiredPropsProjection.Include(attr.ElementName);
                }
            }

            ProjectionDefinition<T> userProj = userProjection.Render(
                BsonSerializer.LookupSerializer<T>(),
                BsonSerializer.SerializerRegistry).Document;

            return Builders<T>.Projection.Combine(new[]
            {
                requiredPropsProjection,
                userProj
            });
        }
    }
}