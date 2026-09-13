
namespace GenjitsuLAB.Core
{
    /// <summary>
    /// Defines the CSV conversion contract used by serializable database values.
    /// </summary>
    public abstract class SerializedCSV
    {
        /// <summary>
        /// Initializes a new CSV-serializable value.
        /// </summary>
        public SerializedCSV()
        {
        }

        /// <summary>
        /// Serializes this value to its CSV cell representation.
        /// </summary>
        /// <returns>The serialized CSV cell value.</returns>
        public abstract string ToCSV();

        /// <summary>
        /// Replaces this value from its CSV cell representation.
        /// </summary>
        /// <param name="text">The serialized CSV cell value.</param>
        public abstract void FromCSV(string text);
    }
}
