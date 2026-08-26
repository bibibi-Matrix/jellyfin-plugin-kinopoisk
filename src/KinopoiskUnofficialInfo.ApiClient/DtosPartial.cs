using System.Collections.Generic;

namespace KinopoiskUnofficialInfo.ApiClient
{
    // Hand-added fields that the generated model misses but backends can supply.

    public partial class Film
    {
        public List<string> Studios { get; set; } = new List<string>();

        /// <summary>
        /// Raw series status: announced/completed/ongoing (kinopoisk.dev).
        /// </summary>
        public string StatusString { get; set; }
    }

    public partial class Season
    {
        public string NameRu { get; set; }

        public string NameEn { get; set; }

        public string AirDate { get; set; }
    }

    public partial class Episode
    {
        /// <summary>
        /// Episode thumbnail url (kinopoisk.dev seasons).
        /// </summary>
        public string StillUrl { get; set; }
    }
}
