using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Raven.Client.Documents.Operations.CompareExchange;
using Raven.Client.Documents.Session;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store
{
    /// <inheritdoc />
    public class FinbuckleRavenDbStore<TTenantInfo> : FinbuckleRavenDbStore<TTenantInfo, UniqueReservation>
        where TTenantInfo : class, ITenantInfo, new()
    {
        /// <summary>
        /// Generates an instance of <see cref="FinbuckleRavenDbStore{TTenantInfo,TUniqueReservation}"/>.
        /// </summary>
        /// <param name="documentSessionProvider"></param>
        /// <param name="uniqueValuesReservationOptions"></param>
        /// <param name="logger"></param>
        public FinbuckleRavenDbStore(
            DocumentSessionProvider documentSessionProvider,
            UniqueValuesReservationOptions uniqueValuesReservationOptions,
            ILogger<FinbuckleRavenDbStore<TTenantInfo>> logger)
            : base(documentSessionProvider, uniqueValuesReservationOptions, logger)
        {
        }

        /// <inheritdoc />
        protected override UniqueReservationDocumentUtility<UniqueReservation> CreateUniqueReservationDocumentsUtility(
            UniqueReservationType reservationType,
            string uniqueValue) => new UniqueReservationDocumentUtility(Session, reservationType, uniqueValue);
    }

    /// <summary>
    /// MultiTenant store based on the RavenDb database.
    /// </summary>
    /// <typeparam name="TTenantInfo">Tenant type.</typeparam>
    /// <typeparam name="TUniqueReservation">Unique values reservation document type.</typeparam>
    public abstract class FinbuckleRavenDbStore<TTenantInfo, TUniqueReservation>
        : IMultiTenantStore<TTenantInfo>, IHavePaginatedMultiTenantStore<TTenantInfo>
        where TTenantInfo : class, ITenantInfo, new()
        where TUniqueReservation : UniqueReservation
    {
        private readonly Func<string, string> _tenantIdentifierNormalizer =
            identifier => identifier.Normalize().ToLowerInvariant();

        /// <summary>
        /// Initializes a new instance of the <see cref="FinbuckleRavenDbStore{TTenantInfo,TUniqueReservation}"/> class.
        /// </summary>
        /// <param name="documentSessionProvider">RavenDb document session provider.</param>
        /// <param name="uniqueValuesReservationOptions">Unique values reservation options.</param>
        /// <param name="logger">Logger.</param>
        protected FinbuckleRavenDbStore(
            DocumentSessionProvider documentSessionProvider,
            UniqueValuesReservationOptions uniqueValuesReservationOptions,
            ILogger<FinbuckleRavenDbStore<TTenantInfo, TUniqueReservation>> logger)
        {
            UniqueValuesReservationOptions = uniqueValuesReservationOptions ??
                                             throw new ArgumentNullException(nameof(uniqueValuesReservationOptions));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            Session = documentSessionProvider?.Invoke() ??
                      throw new ArgumentNullException(nameof(documentSessionProvider));
        }

        /// <summary>
        /// Gets the document session.
        /// </summary>
        protected IAsyncDocumentSession Session { get; }

        /// <summary>
        /// Gets the unique value representation options.
        /// </summary>
        protected UniqueValuesReservationOptions UniqueValuesReservationOptions { get; }

        /// <summary>
        /// Gets the logger.
        /// </summary>
        protected ILogger<FinbuckleRavenDbStore<TTenantInfo, TUniqueReservation>> Logger { get; }

        /// <inheritdoc/>
        public virtual async Task<bool> TryAddAsync(TTenantInfo tenantInfo)
        {
            if (tenantInfo == null)
            {
                throw new ArgumentNullException(nameof(tenantInfo));
            }

            CheckRequiredFieldsOrThrow(tenantInfo);

            // cluster wide as we will deal with compare exchange values either directly or as atomic guards
            // for unique value reservations
            Session.Advanced.SetTransactionMode(TransactionMode.ClusterWide);
            Session.Advanced.UseOptimisticConcurrency = false; // cluster wide tx doesn't support opt. concurrency

            // no change vector as we rely on cluster wide optimistic concurrency and atomic guards
            await Session.StoreAsync(tenantInfo).ConfigureAwait(false);

            // handle unique reservation
            string tenantIdentifierNormalized = _tenantIdentifierNormalizer(tenantInfo.Identifier);
            if (UniqueValuesReservationOptions.UseReservationDocumentsForUniqueValues)
            {
                UniqueReservationDocumentUtility<TUniqueReservation> uniqueReservationUtil =
                    CreateUniqueReservationDocumentsUtility(
                        UniqueReservationType.Identifier,
                        tenantIdentifierNormalized
                    );
                bool uniqueExists = await uniqueReservationUtil.CheckIfUniqueIsTakenAsync().ConfigureAwait(false);
                if (uniqueExists)
                {
                    Logger.LogInformation("Tenant identifier `{Identifier}` not unique", tenantInfo.Identifier);
                    return false;
                }

                await uniqueReservationUtil
                    .CreateReservationDocumentAddToUnitOfWorkAsync(tenantInfo.Id)
                    .ConfigureAwait(false);
            }
            else
            {
                CompareExchangeUtility compareExchangeUtility = CreateCompareExchangeUtility();
                string compareExchangeKey = compareExchangeUtility.CreateCompareExchangeKey(
                    UniqueReservationType.Identifier,
                    tenantIdentifierNormalized
                );

                CompareExchangeValue<string>? existingCompareExchange = await Session
                    .Advanced
                    .ClusterTransaction
                    .GetCompareExchangeValueAsync<string>(compareExchangeKey)
                    .ConfigureAwait(false);

                if (existingCompareExchange != null)
                {
                    Logger.LogInformation("Tenant identifier `{Identifier}` not unique", tenantInfo.Identifier);
                    return false;
                }

                Session
                    .Advanced
                    .ClusterTransaction
                    .CreateCompareExchangeValue(
                        compareExchangeKey,
                        tenantInfo.Id
                    );
            }

            try
            {
                await Session.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Error adding new Tenant entity. Error: {Message}",
                    ex.Message
                );
            }

            return false;
        }

        /// <inheritdoc/>
        public virtual async Task<bool> TryUpdateAsync(TTenantInfo tenantInfo)
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

            // check if normalized name has changed, and if yes make sure it's unique by reserving it
            string tenantIdentifierNormalized = _tenantIdentifierNormalizer(tenantInfo.Identifier);
            if (Session.IfPropertyChanged(
                    tenantInfo,
                    changedPropertyName: nameof(tenantInfo.Identifier),
                    newPropertyValue: tenantIdentifierNormalized,
                    out PropertyChange<string>? propertyChange
                ))
            {
                Debug.Assert(propertyChange != null, $"Unexpected NULL value for {nameof(propertyChange)}");

                // cluster wide as we will deal with compare exchange values either directly or as atomic guards
                Session.Advanced.SetTransactionMode(TransactionMode.ClusterWide);
                Session.Advanced.UseOptimisticConcurrency = false; // cluster wide tx doesn't support opt. concurrency

                if (UniqueValuesReservationOptions.UseReservationDocumentsForUniqueValues)
                {
                    UniqueReservationDocumentUtility<TUniqueReservation> uniqueReservationUtil =
                        CreateUniqueReservationDocumentsUtility(
                            UniqueReservationType.Identifier,
                            tenantIdentifierNormalized
                        );
                    bool uniqueExists = await uniqueReservationUtil.CheckIfUniqueIsTakenAsync().ConfigureAwait(false);
                    if (uniqueExists)
                    {
                        Logger.LogInformation(
                            "Compare exchange unique value {Identifier} already exists",
                            tenantInfo.Identifier
                        );
                        return false;
                    }

                    await uniqueReservationUtil.UpdateReservationAndAddToUnitOfWork(
                        oldUniqueValue: propertyChange.OldPropertyValue,
                        ownerDocumentId: tenantInfo.Id
                    ).ConfigureAwait(false);
                }
                else
                {
                    CompareExchangeUtility compareExchangeUtility = CreateCompareExchangeUtility();
                    bool reservationUpdatePrepared = await compareExchangeUtility
                        .PrepareReservationUpdateInUnitOfWorkAsync(
                            uniqueReservationType: UniqueReservationType.Identifier,
                            newUniqueValueNormalized: tenantIdentifierNormalized,
                            oldUniqueValueNormalized: propertyChange.OldPropertyValue,
                            compareExchangeValue: tenantInfo.Id,
                            logger: Logger,
                            cancellationToken: default
                        ).ConfigureAwait(false);
                    if (!reservationUpdatePrepared)
                    {
                        Logger.LogInformation(
                            "Compare exchange unique value {Identifier} already exists",
                            tenantInfo.Identifier
                        );
                        return false;
                    }
                }
            }

            try
            {
                // in cluster wide mode relying on optimistic concurrency using atomic guards
                if (((AsyncDocumentSession)Session).TransactionMode != TransactionMode.ClusterWide)
                {
                    string changeVector = Session.Advanced.GetChangeVectorFor(tenantInfo);
                    await Session.StoreAsync(tenantInfo, changeVector, tenantInfo.Id)
                        .ConfigureAwait(false);
                }

                await Session.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Error updating Tenant entity. Error: {Message}",
                    ex.Message
                );
            }

            return false;
        }

        /// <inheritdoc />
        public virtual async Task<bool> TryRemoveAsync(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException(nameof(tenantId) + " must not be empty.");
            }

            TTenantInfo? entity = await Session.LoadAsync<TTenantInfo>(tenantId);
            if (entity is null)
            {
                Logger.LogError(
                    "Error removing Tenant entity as entity was not found by id {TenantId}",
                    tenantId
                );
                return false;
            }

            // cluster wide as we will deal with compare exchange values either directly or as atomic guards
            Session.Advanced.SetTransactionMode(TransactionMode.ClusterWide);
            Session.Advanced.UseOptimisticConcurrency = false; // cluster wide tx doesn't support opt. concurrency

            string tenantIdentifierNormalized = _tenantIdentifierNormalizer(entity.Identifier);
            if (UniqueValuesReservationOptions.UseReservationDocumentsForUniqueValues)
            {
                UniqueReservationDocumentUtility<TUniqueReservation> uniqueReservationUtil =
                    CreateUniqueReservationDocumentsUtility(
                        UniqueReservationType.Identifier,
                        tenantIdentifierNormalized
                    );
                await uniqueReservationUtil.MarkReservationForDeletionAsync().ConfigureAwait(false);
            }
            else
            {
                CompareExchangeUtility compareExchangeUtility = CreateCompareExchangeUtility();
                await compareExchangeUtility.PrepareReservationForRemovalAsync(
                    UniqueReservationType.Identifier,
                    tenantIdentifierNormalized,
                    Logger,
                    default
                ).ConfigureAwait(false);
            }

            try
            {
                // for optimistic concurrency relying on atomic guards and cluster-wide sessions
                Session.Delete(entity.Id);
                await Session.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Error deleting Tenant entity. Error: {Message}",
                    ex.Message
                );
            }

            return false;
        }

        /// <inheritdoc />
        public virtual Task<TTenantInfo> TryGetByIdentifierAsync(string identifier)
        {
            return Queryable.Where(Session.Query<TTenantInfo>(), entity => entity.Identifier.Equals(identifier))
                .SingleOrDefaultAsync();
        }

        /// <inheritdoc />
        public virtual Task<TTenantInfo> TryGetAsync(string id)
        {
            return Session.LoadAsync<TTenantInfo>(id);
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<TTenantInfo>> GetAllAsync()
        {
            IRavenQueryable<TTenantInfo> query = Session.Query<TTenantInfo>();
            IAsyncEnumerator<StreamResult<TTenantInfo>> results = await Session.Advanced
                .StreamAsync(query)
                .ConfigureAwait(false);

            ICollection<TTenantInfo> allTenants = new List<TTenantInfo>();
            while (await results.MoveNextAsync().ConfigureAwait(false))
            {
                allTenants.Add(results.Current.Document);
            }

            return allTenants;
        }

        /// <inheritdoc />
        public virtual async Task<PaginatedResult<TTenantInfo>> GetAllPaginatedAsync(int page, int itemsPerPage)
        {
            if (page < 1)
            {
                throw new ArgumentException($"Argument {nameof(page)} must not be lower than 1.");
            }

            if (itemsPerPage < 1)
            {
                throw new ArgumentException($"Argument {nameof(itemsPerPage)} must not be lower than 1.");
            }

            List<TTenantInfo> result = await Session.Query<TTenantInfo>()
                .Statistics(out QueryStatistics stats)
                .Skip((page - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToListAsync();

            return new PaginatedResult<TTenantInfo>(stats.TotalResults, result);
        }

        /// <summary>
        /// Checks required fields on provided tenant object.
        /// </summary>
        /// <param name="tenantInfo">Tenant object to check for required fields.</param>
        /// <exception cref="ArgumentException">If any property does not match criteria.</exception>
        protected virtual void CheckRequiredFieldsOrThrow(TTenantInfo tenantInfo)
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

        /// <summary>
        /// Create an instance of <see cref="UniqueReservationDocumentUtility"/>.
        /// </summary>
        /// <param name="reservationType"></param>
        /// <param name="uniqueValue"></param>
        /// <returns>Instance of <see cref="UniqueReservationDocumentUtility"/>.</returns>
        protected abstract UniqueReservationDocumentUtility<TUniqueReservation> CreateUniqueReservationDocumentsUtility(
            UniqueReservationType reservationType,
            string uniqueValue);
    }
}