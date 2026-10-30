using FileHub.BusinessLogic.Media;

namespace FileHub.IntegrationTests;

/// <summary>What a folder name is reduced to before it is searched for. Pure, so every case is a line.</summary>
public sealed class FolderTitleParserTests
{
    [Theory]
    [InlineData("The Matrix (1999)", "The Matrix", 1999)]
    [InlineData("Dune Part Two (2024)", "Dune Part Two", 2024)]
    [InlineData("Dune.Part.Two.2024.1080p.BluRay.x264-GROUP", "Dune Part Two", 2024)]
    [InlineData("Dune.Part.Two.2024.German.DL.1080p.WEB-DL.x265", "Dune Part Two", 2024)]
    [InlineData("Blade Runner 2049 (2017)", "Blade Runner 2049", 2017)]
    [InlineData("2001 A Space Odyssey (1968)", "2001 A Space Odyssey", 1968)]
    [InlineData("Scrubs (2001) {tmdb-4556}", "Scrubs", 2001)]
    [InlineData("Scrubs.2001.S01.COMPLETE.1080p", "Scrubs", 2001)]
    [InlineData("Breaking Bad S01-S05", "Breaking Bad", null)]
    [InlineData("Cyberpunk 2077 [FitGirl Repack]", "Cyberpunk 2077", null)]
    [InlineData("Elden.Ring-RUNE", "Elden Ring", null)]
    [InlineData("Elden Ring v1.10.1 (GOG)", "Elden Ring", null)]
    [InlineData("Baldurs_Gate_3_Build_12345", "Baldurs Gate 3", null)]
    [InlineData("Spider-Man Remastered", "Spider-Man", null)]
    [InlineData("Zum Glück gibt's Schreiner (2020)", "Zum Glück gibt's Schreiner", 2020)]
    [InlineData("1080p", "1080p", null)]
    public void Reduces_a_folder_name_to_its_title_and_year(string folder, string title, int? year)
    {
        var parsed = FolderTitleParser.Parse(folder);

        Assert.Equal(title, parsed.Title);
        Assert.Equal(year, parsed.Year);
    }

    [Fact]
    public void An_id_tag_pins_the_match()
    {
        Assert.Equal(4556, FolderTitleParser.Parse("Scrubs (2001) {tmdb-4556}").TmdbId);
        Assert.Null(FolderTitleParser.Parse("Scrubs (2001) {tmdb-4556}").TmdbType);
        Assert.Equal(("tv", 295778L), ParsedTmdb("Scrubs (2026) {tmdb-tv-295778}"));
        Assert.Equal(("movie", 693134L), ParsedTmdb("Dune {TMDB-Movie-693134}"));
        Assert.Equal("Scrubs", FolderTitleParser.Parse("Scrubs (2026) {tmdb-tv-295778}").Title);
        Assert.Equal(1020, FolderTitleParser.Parse("GTA V {igdb-1020}").IgdbId);
        Assert.Null(FolderTitleParser.Parse("GTA V {igdb-1020}").TmdbId);
        Assert.Null(FolderTitleParser.Parse("Scrubs (2001)").TmdbId);
    }

    private static (string? Type, long? Id) ParsedTmdb(string folder)
    {
        var parsed = FolderTitleParser.Parse(folder);
        return (parsed.TmdbType, parsed.TmdbId);
    }
}
