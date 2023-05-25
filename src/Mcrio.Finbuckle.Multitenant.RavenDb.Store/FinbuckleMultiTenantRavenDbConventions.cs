using System;
using Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store
{
    /// <summary>
    /// Produces predefined collection names for implemented entity types.
    /// </summary>
    public static class FinbuckleMultiTenantRavenDbConventions
    {
        /// <summary>
        /// Get collection name for Identity Server on RavenDb known types.
        /// </summary>
        /// <param name="type">Object type to get the collection for.</param>
        /// <param name="collectionName">Optional collection name if found.</param>
        /// <returns>Default collection name if known type otherwise Null.</returns>
        public static bool TryGetCollectionName(Type type, out string? collectionName)
        {
            if (typeof(UniqueReservation).IsAssignableFrom(type))
            {
                collectionName = "FbMtUniqueReservations";
                return true;
            }

            collectionName = null;
            return false;
        }
    }
}