using System.Collections.Generic;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class FilmImagesResponse
    {
        public int Total { get; set; }

        public ICollection<FilmImage> Items { get; set; } = new List<FilmImage>();
    }

    public class FilmImage
    {
        public string ImageUrl { get; set; }

        public string ImagePreviewUrl { get; set; }
    }
}
