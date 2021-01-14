using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Interfaces;
using Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Model;
using Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store
{
    /// <summary>
    /// MultiTenant store based on the RavenDb database.
    /// </summary>
    /// <typeparam name="T">Tenant type.</typeparam>
    public class FinbuckleRavenDbStore<T> : IMultiTenantStore<T>, IHavePaginatedMultiTenantStore<T>
        where T : class, ITenantInfo, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FinbuckleRavenDbStore{T}"/> class.
        /// </summary>
        /// <param name="documentSessionProvider">RavenDb document session provider.</param>
        /// <param name="logger">Logger.</param>
        public FinbuckleRavenDbStore(
            DocumentSessionProvider documentSessionProvider,
            ILogger<FinbuckleRavenDbStore<T>> logger)
        {
            Logger = logger;
            Session = documentSessionProvider();
        }

        /// <summary>
        /// Gets the document session.
        /// </summary>
        protected IAsyncDocumentSession Session { get; }

        /// <summary>
        /// Gets the logger.
        /// </summary>
        protected ILogger<FinbuckleRavenDbStore<T>> Logger { get; }

        /// <inheritdoc/>
        public async Task<bool> TryAddAsync(T tenantInfo)
        {
            if (tenantInfo == null)
            {
                throw new ArgumentNullException(nameof(tenantInfo));
            }

            CheckRequiredFieldsOrThrow(tenantInfo);

            CompareExchangeUtility compareExchangeUtility = CreateCompareExchangeUtility();
            var entitySaveSuccess = false;
            var identifierReserveSuccess = false;

            try
            {
                await Session.StoreAsync(tenantInfo, string.Empty, tenantInfo.Id)
                    .ConfigureAwait(false);

                identifierReserveSuccess = await compareExchangeUtility.CreateReservationAsync(
                    CompareExchangeUtility.ReservationType.Identifier,
                    tenantInfo,
                    tenantInfo.Identifier,
                    tenantInfo.Id
                ).ConfigureAwait(false);

                if (identifierReserveSuccess == false)
                {
                    throw new Exception("Tenant identifier not unique");
                }

                await Session.SaveChangesAsync().ConfigureAwait(false);
                entitySaveSuccess = true;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding new Tenant entity. Error: {}", ex.Message);
            }
            finally
            {
                if (entitySaveSuccess == false && identifierReserveSuccess)
                {
                    Logger.LogDebug(
                        "Tenant create failed persisting entity. Deleting related identifier compare exchange key."
                    );
                    await compareExchangeUtility.RemoveReservationAsync(
                        CompareExchangeUtility.ReservationType.Identifier,
                        tenantInfo,
                        tenantInfo.Identifier
                    ).ConfigureAwait(false);
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public async Task<bool> TryUpdateAsync(T tenantInfo)
        {
            if (tenantInfo == null)
            {
                throw new ArgumentNullException(nameof(tenantInfo));
            }

            CheckRequiredFieldsOrThrow(tenantInfo);

            if (!Session.Advanced.IsLoaded(tenantInfo.Id))
            {
                throw new Exception("Tenant is expected to be already loaded in the RavenDB session.");
            }

            CompareExchangeUtility compareExchangeUtility = CreateCompareExchangeUtility();

            // if identifier has changed, and if yes make sure it's unique by reserving it
            PropertyChange<string>? identifierChange = null;

            var updateSuccess = false;

            try
            {
                identifierChange = await Session.ReserveIfPropertyChangedAsync(
                    compareExchangeUtility,
                    tenantInfo,
                    nameof(tenantInfo.Identifier),
                    tenantInfo.Identifier,
                    tenantInfo.Identifier,
                    CompareExchangeUtility.ReservationType.Identifier,
                    tenantInfo.Id
                ).ConfigureAwait(false);

                string changeVector = Session.Advanced.GetChangeVectorFor(tenantInfo);
                await Session.StoreAsync(tenantInfo, changeVector, tenantInfo.Id)
                    .ConfigureAwait(false);
                await Session.SaveChangesAsync().ConfigureAwait(false);

                updateSuccess = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating Tenant entity. Error: {}", ex.Message);
            }
            finally
            {
                if (identifierChange != null)
                {
                    string identifierReservationToRemove = updateSuccess
                        ? identifierChange.OldPropertyValue
                        : identifierChange.NewPropertyValue;

                    bool removeResult = await compareExchangeUtility.RemoveReservationAsync(
                        CompareExchangeUtility.ReservationType.Identifier,
                        tenantInfo,
                        identifierReservationToRemove
                    ).ConfigureAwait(false);
                    if (!removeResult)
                    {
                        Logger.LogError(
                            $"Failed removing identifier '{identifierReservationToRemove}' from compare exchange "
                        );
                    }
                }
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<bool> TryRemoveAsync(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException(nameof(tenantId) + " must not be empty.");
            }

            T? entity = await Session.LoadAsync<T>(tenantId);
            if (entity is null)
            {
                Logger.LogError("Error removing Tenant entity as entity was not found by id {}", tenantId);
                return false;
            }

            var deleteSuccess = false;

            try
            {
                string changeVector = Session.Advanced.GetChangeVectorFor(entity);
                Session.Delete(entity.Id, changeVector);
                await Session.SaveChangesAsync().ConfigureAwait(false);
                deleteSuccess = true;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting Tenant entity. Error: {}", ex.Message);
            }
            finally
            {
                if (deleteSuccess)
                {
                    CompareExchangeUtility compareExchangeUtility = CreateCompareExchangeUtility();

                    bool removeIdentifierCmpE = await compareExchangeUtility.RemoveReservationAsync(
                        CompareExchangeUtility.ReservationType.Identifier,
                        entity,
                        entity.Identifier
                    ).ConfigureAwait(false);
                    if (!removeIdentifierCmpE)
                    {
                        Logger.LogError(
                            $"Failed removing tenant identifier '{entity.Identifier}' from compare exchange "
                        );
                    }
                }
            }

            return false;
        }

        /// <inheritdoc />
        public Task<T> TryGetByIdentifierAsync(string identifier)
        {
            return Queryable.Where(Session.Query<T>(), entity => entity.Identifier.Equals(identifier))
                .SingleOrDefaultAsync();
        }

        /// <inheritdoc />
        public Task<T> TryGetAsync(string id)
        {
            return Session.LoadAsync<T>(id);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            IRavenQueryable<T> query = Session.Query<T>();
            IAsyncEnumerator<StreamResult<T>> results = await Session.Advanced
                .StreamAsync(query)
                .ConfigureAwait(false);

            ICollection<T> allTenants = new List<T>();
            while (await results.MoveNextAsync().ConfigureAwait(false))
            {
                allTenants.Add(results.Current.Document);
            }

            return allTenants;
        }

        /// <inheritdoc />
        public async Task<PaginatedResult<T>> GetAllPaginatedAsync(int page, int itemsPerPage)
        {
            if (page < 1)
            {
                throw new ArgumentException($"Argument {nameof(page)} must not be lower than 1.");
            }

            if (itemsPerPage < 1)
            {
                throw new ArgumentException($"Argument {nameof(itemsPerPage)} must not be lower than 1.");
            }

            List<T> result = await Session.Query<T>()
                .Statistics(out QueryStatistics stats)
                .Skip((page - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToListAsync();

            return new PaginatedResult<T>(stats.TotalResults, result);
        }

        /// <summary>
        /// Checks required fields on provided tenant object.
        /// </summary>
        /// <param name="tenantInfo">Tenant object to check for required fields.</param>
        /// <exception cref="ArgumentException">If any property does not match criteria.</exception>
        protected virtual void CheckRequiredFieldsOrThrow(T tenantInfo)
        {
            if (string.IsNullOrWhiteSpace(tenantInfo.Identifier))
            {
                throw new ArgumentException(nameof(tenantInfo.Identifier) + " must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(tenantInfo.Name))
            {
                throw new ArgumentException(nameof(tenantInfo.Name) + " must not be empty.");
            }
        }

        /// <summary>
        /// Creates a <see cref="CompareExchangeUtility"/> object.
        /// </summary>
        /// <returns>Instance of <see cref="CompareExchangeUtility"/>.</returns>
        protected virtual CompareExchangeUtility CreateCompareExchangeUtility()
        {
            return new CompareExchangeUtility(Session);
        }
    }
}