using System.ComponentModel.DataAnnotations;

using TorrenClou.Core.Configuration;

namespace TorrenClou.Core.Options
{
    /// <summary>Bound from "ConnectionStrings". Where the database lives.</summary>
    public class DatabaseOptions
    {
        public const string SectionName = "ConnectionStrings";

        /// <summary>
        /// The Npgsql connection string. The three worker processes have no
        /// appsettings.json at all, so for them this has never had a fallback of
        /// any kind — an unset value simply failed somewhere deeper.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "A database connection string is required.")]
        [ConfigDoc("POSTGRES_PASSWORD",
            Description = "Assembled by the entrypoint from POSTGRES_DB, POSTGRES_USER and POSTGRES_PASSWORD.",
            Default = "password generated on first run; database torrenclo, user torrenclo_user",
            Secret = true)]
        public string DefaultConnection { get; set; } = string.Empty;
    }
}
