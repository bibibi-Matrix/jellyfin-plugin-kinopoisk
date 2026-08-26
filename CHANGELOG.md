# Changelog

## [10.11.11.2]

- Search results now show year for better disambiguation (e.g. "Чудо-доктор (2026)")
- VideoResolver: type filtering — prefers TV_SERIES for Series, FILM for Movie
- VideoResolver: year+type combined filtering before falling back
- Kinopoisk.dev adapter now returns type in search results
- KinopoiskExternalId supports Season and Episode in Identify dialog

## [10.11.11.1]

- Fix kinopoisk.dev season limit 1000 → 250 (API max)
- SeasonMetadataProvider: season name/description from KP
- Season poster fallback to series poster
- Full card: episode stills, logo/cover images, thumb screenshots, series status, RU premiere priority, studios
- Fixed poster sanitizer, age rating prefix, runtime parsing
- Pluggable backend: unofficial + kinopoisk.dev dropdown