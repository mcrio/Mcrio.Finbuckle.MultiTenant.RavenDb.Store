namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb
{
    /// <summary>
    /// Unique reservation document.
    /// </summary>
    public class UniqueReservation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UniqueReservation"/> class.
        /// </summary>
        /// <param name="id">Unique reservation ID.</param>
        /// <param name="referenceId">Reference document id.</param>
        public UniqueReservation(string id, string referenceId)
        {
            Id = id;
            ReferenceId = referenceId;
        }

        /// <summary>
        /// Required for ORM.
        /// </summary>
#pragma warning disable CS8618
        protected UniqueReservation()
#pragma warning restore CS8618
        {
        }

        /// <summary>
        /// Gets the reservation id.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the reference document id.
        /// </summary>
        public string ReferenceId { get; private set; }
    }
}