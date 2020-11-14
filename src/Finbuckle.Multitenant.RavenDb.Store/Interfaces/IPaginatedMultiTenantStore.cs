using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Model;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Interfaces
{
    /// <summary>
    /// Defines method for retrieving tenant data with pagination.
    /// </summary>
    /// <typeparam name="TTenantInfo">Tenant-info type.</typeparam>
    public interface IPaginatedMultiTenantStore<TTenantInfo>
        where TTenantInfo : ITenantInfo
    {
        /// <summary>
        /// Gets all tenants paginated.
        /// </summary>
        /// <param name="page">Page starting with 1.</param>
        /// <param name="itemsPerPage">How many items per page to retrieve.</param>
        /// <returns>Instance of <see cref="PaginatedResult{TTenantInfo}"/>.</returns>
        Task<PaginatedResult<TTenantInfo>> GetAllPaginatedAsync(int page, int itemsPerPage);
    }
}