using System.Collections.Generic;
using Finbuckle.MultiTenant;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Model
{
    /// <summary>
    /// Contains data about paginated search.
    /// </summary>
    /// <typeparam name="TTenantInfo">Tenant-info type.</typeparam>
    public class PaginatedResult<TTenantInfo>
        where TTenantInfo : ITenantInfo
    {
        internal PaginatedResult(int totalItemsCount, IEnumerable<TTenantInfo> items)
        {
            TotalItemsCount = totalItemsCount;
            Items = items;
        }

        /// <summary>
        /// Gets the total items count.
        /// </summary>
        public int TotalItemsCount { get; }

        /// <summary>
        /// Gets the items for the requested page.
        /// </summary>
        public IEnumerable<TTenantInfo> Items { get; }
    }
}