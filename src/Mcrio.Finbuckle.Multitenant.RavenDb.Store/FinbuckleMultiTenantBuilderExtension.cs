using System;
using Finbuckle.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Raven.Client.Documents.Session;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store
{
    /// <summary>
    /// Locates the document session.
    /// </summary>
    /// <param name="provider">Service provider.</param>
    /// <returns>Instance of an RavenDB async document session.</returns>
    public delegate IAsyncDocumentSession DocumentSessionServiceLocator(IServiceProvider provider);

    /// <summary>
    /// Extension methods to <see cref="FinbuckleMultiTenantBuilder{TTenantInfo}"/> for adding RavenDB stores.
    /// </summary>
    public static class FinbuckleMultiTenantBuilderExtension
    {
        /// <summary>
        /// Adds the RavenDb implementation of Finbuckle MultiTenant store.
        /// </summary>
        /// <param name="builder">Multi-tenant builder.</param>
        /// <param name="documentSessionServiceLocator">RavenDb document session provider.</param>
        /// <param name="uniqueValuesReservationOptionsConfig">Configure Unique value reservations options.</param>
        /// <typeparam name="TTenantInfo">Tenant type.</typeparam>
        /// <returns>Multi tenant builder.</returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> WithRavenDbStore<TTenantInfo>(
            this FinbuckleMultiTenantBuilder<TTenantInfo> builder,
            DocumentSessionServiceLocator documentSessionServiceLocator,
            Action<UniqueValuesReservationOptions>? uniqueValuesReservationOptionsConfig = null)
            where TTenantInfo : class, ITenantInfo, new()
        {
            return WithRavenDbStore<TTenantInfo, FinbuckleRavenDbStore<TTenantInfo>>(
                builder,
                documentSessionServiceLocator,
                uniqueValuesReservationOptionsConfig
            );
        }

        /// <summary>
        /// Adds the RavenDb implementation of Finbuckle MultiTenant store.
        /// </summary>
        /// <param name="builder">Multi-tenant builder.</param>
        /// <param name="documentSessionServiceLocator">RavenDb document session provider.</param>
        /// <param name="uniqueValuesReservationOptionsConfig">Configure Unique value reservations options.</param>
        /// <typeparam name="TTenantInfo">Tenant type.</typeparam>
        /// <typeparam name="TRavenDbStore">RavenDb store type.</typeparam>
        /// <returns>Multi tenant builder.</returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> WithRavenDbStore<
            TTenantInfo,
            TRavenDbStore>(
            this FinbuckleMultiTenantBuilder<TTenantInfo> builder,
            DocumentSessionServiceLocator documentSessionServiceLocator,
            Action<UniqueValuesReservationOptions>? uniqueValuesReservationOptionsConfig = null)
            where TTenantInfo : class, ITenantInfo, new()
            where TRavenDbStore : IMultiTenantStore<TTenantInfo>
        {
            var uniqueValueRelatedOptions = new UniqueValuesReservationOptions();
            uniqueValuesReservationOptionsConfig?.Invoke(uniqueValueRelatedOptions);
            builder.Services.TryAddSingleton(uniqueValueRelatedOptions);

            builder.Services.TryAddScoped<DocumentSessionProvider>(
                provider => () => documentSessionServiceLocator(provider)
            );
            builder.WithStore<TRavenDbStore>(ServiceLifetime.Scoped);

            return builder;
        }
    }
}