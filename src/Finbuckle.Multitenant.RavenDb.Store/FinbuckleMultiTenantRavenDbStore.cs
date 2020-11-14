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
    public class FinbuckleRavenDbStore<T> : IMultiTenantStore<T>, IPaginatedMultiTenantStore<T>
        where T : class, ITenantInfo, new()
    {
        private readonly ILogger<FinbuckleRavenDbStore<T>> _logger;
        private readonly IAsyncDocumentSession _documentSession;

        /// <summary>
        /// Initializes a new instance of the <see cref="FinbuckleRavenDbStore{T}"/> class.
        /// </summary>
        /// <param name="documentSessionProvider">RavenDb document session provider.</param>
        /// <param name="logger">Logger.</param>
        public FinbuckleRavenDbStore(
            DocumentSessionProvider documentSessionProvider,
            ILogger<FinbuckleRavenDbStore<T>> logger)
        {
            _logger = logger;
            _documentSession = documentSessionProvider();
        }

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
                await _documentSession.StoreAsync(tenantInfo, string.Empty, tenantInfo.Id)
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

                await _documentSession.SaveChangesAsync().ConfigureAwait(false);
                entitySaveSuccess = true;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new Tenant entity. Error: {}", ex.Message);
            }
            finally
            {
                if (entitySaveSuccess == false && identifierReserveSuccess)
                {
                    _logger.LogDebug(
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

            if (!_documentSession.Advanced.IsLoaded(tenantInfo.Id))
            {
                throw new Exception("Tenant is expected to be already loaded in the RavenDB session.");
            }

            CompareExchangeUtility compareExchangeUtility = CreateCompareExchangeUtility();

            // if identifier has changed, and if yes make sure it's unique by reserving it
            PropertyChange<string>? identifierChange = null;

            var updateSuccess = false;

            try
            {
                identifierChange = await _documentSession.ReserveIfPropertyChangedAsync(
                    compareExchangeUtility,
                    tenantInfo,
                    nameof(tenantInfo.Identifier),
                    tenantInfo.Identifier,
                    tenantInfo.Identifier,
                    CompareExchangeUtility.ReservationType.Identifier,
                    tenantInfo.Id
                ).ConfigureAwait(false);

                string changeVector = _documentSession.Advanced.GetChangeVectorFor(tenantInfo);
                await _documentSession.StoreAsync(tenantInfo, changeVector, tenantInfo.Id)
                    .ConfigureAwait(false);
                await _documentSession.SaveChangesAsync().ConfigureAwait(false);

                updateSuccess = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Tenant entity. Error: {}", ex.Message);
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
                        _logger.LogError(
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

            T? entity = await _documentSession.LoadAsync<T>(tenantId);
            if (entity is null)
            {
                _logger.LogError("Error removing Tenant entity as entity was not found by id {}", tenantId);
                return false;
            }

            var deleteSuccess = false;

            try
            {
                string changeVector = _documentSession.Advanced.GetChangeVectorFor(entity);
                _documentSession.Delete(entity.Id, changeVector);
                await _documentSession.SaveChangesAsync().ConfigureAwait(false);
                deleteSuccess = true;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Tenant entity. Error: {}", ex.Message);
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
                        _logger.LogError(
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
            return Queryable.Where(_documentSession.Query<T>(), entity => entity.Identifier.Equals(identifier))
                .SingleOrDefaultAsync();
        }

        /// <inheritdoc />
        public Task<T> TryGetAsync(string id)
        {
            return _documentSession.LoadAsync<T>(id);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            IRavenQueryable<T> query = _documentSession.Query<T>();
            IAsyncEnumerator<StreamResult<T>> results = await _documentSession.Advanced
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

            List<T> result = await _documentSession.Query<T>()
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
            return new CompareExchangeUtility(_documentSession);
        }
    }
}