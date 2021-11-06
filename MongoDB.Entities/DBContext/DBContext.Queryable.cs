﻿using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace MongoDB.Entities
{
    public partial class DBContext
    {
        /// <summary>
        /// Exposes the MongoDB collection for the given entity type as IQueryable in order to facilitate LINQ queries
        /// </summary>
        /// <param name="options">The aggregate options</param>
        /// <typeparam name="T">The type of entity</typeparam>
        /// <param name="ignoreGlobalFilters">Set to true if you'd like to ignore any global filters for this operation</param>
        public IMongoQueryable<T> Queryable<T>(AggregateOptions? options = null, bool ignoreGlobalFilters = false) where T : IEntity
        {
            var globalFilter = Logic.MergeWithGlobalFilter(ignoreGlobalFilters, _globalFilters, Builders<T>.Filter.Empty);

            var q = Session == null
               ? CollectionFor<T>().AsQueryable(options)
               : CollectionFor<T>().AsQueryable(Session, options);

            if (globalFilter != Builders<T>.Filter.Empty)
            {
                q = q.Where(_ => globalFilter.Inject());
            }

            return q;
        }
    }
}
