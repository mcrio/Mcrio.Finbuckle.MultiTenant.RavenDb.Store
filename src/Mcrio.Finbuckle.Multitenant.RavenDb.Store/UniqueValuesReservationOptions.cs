namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store
{
    /// <summary>
    /// Unique values reservation related options.
    /// </summary>
    public class UniqueValuesReservationOptions
    {
        /// <summary>
        /// When TRUE reservation documents and RavenDB atomic guards are used for cluster wide
        /// unique value reservation.
        /// If FALSE Compare Exchange is used for cluster wide unique value reservation.
        /// Note: Reservation documents can be part of database replication while compare exchange
        /// values, and atomic guards which are compare exchange values as well, cannot be part
        /// of database replication.
        /// See here <see href="https://ravendb.net/docs/article-page/5.4/csharp/client-api/operations/compare-exchange/overview#why-compare-exchange-items-are-not-replicated-to-external-databases"/>
        /// why compare exchange values are not externally replicated.
        ///
        /// NOTE: Change with caution if already in production!.
        /// </summary>
        public bool UseReservationDocumentsForUniqueValues { get; set; } = false;
    }
}