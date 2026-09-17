using System.Xml.Linq;
using Alloy.Common;
using AlloyClient.Data;
using AlloyClient.Screens.Components.CharacterList;
using AlloyClient.Ui;
using AlloyClient.Ui.Components.Graphics;
using AlloyClient.Ui.Flash;

namespace Alloy.UiLib.Tests;

internal static class CharacterSelectionTests {
    public static void Run() {
        var guest = XElement.Parse("""
            <Chars nextCharId="0" maxNumChars="1">
              <Account><AccountId>0</AccountId><Name/><Stats><Fame>0</Fame><Credits>0</Credits></Stats></Account>
              <News><Item><Icon>oryx</Icon><Title>News title</Title><TagLine>News tagline</TagLine><Link>https://example.com/</Link><Date>1589385002</Date></Item></News>
              <OwnedSkins/>
            </Chars>
            """);
        var data = new CharacterListData(guest);
        Equal(0, data.Characters.Length);
        Equal(1, data.AvailableSlots);
        Equal(2000, data.CharSlotCost);
        Equal(0, data.OwnedSkins.Length);
        var news = NewsData.FromCharacterList(guest);
        Equal(1, news.NewsList.Length);
        Equal("oryx", news.NewsList[0].Icon);
        Equal("News title", news.NewsList[0].Title);
        Equal("News tagline", news.NewsList[0].TagLine);
        Equal(true, news.NewsList[0].TryGetWebLink(out _));
        var account = new AccountData(guest.Element("Account")!);
        Equal(0, account.Stats.ClassStats.Length);

        var characters = new CharacterListData(XElement.Parse("""
            <Chars maxNumChars="2" nextCharId="4">
              <Char id="1"><ObjectType>768</ObjectType><Level>3</Level><Exp>100</Exp><CurrentFame>1</CurrentFame></Char>
              <Char id="2"><ObjectType>782</ObjectType><Level>20</Level><Exp>50000</Exp><CurrentFame>170</CurrentFame><Texture>1024</Texture><Tex1>123</Tex1><Tex2>456</Tex2><Equipment>1,2,-1,4</Equipment><MaxHitPoints>700</MaxHitPoints><HitPoints>650</HitPoints><MaxMagicPoints>252</MaxMagicPoints><MagicPoints>200</MagicPoints></Char>
            </Chars>
            """));
        Equal(2, characters.Characters[0].Id);
        Equal(0, characters.AvailableSlots);
        var character = characters.Characters[0];
        Equal(20, character.Level);
        Equal(50000, character.Experience);
        Equal((ushort)1024, character.Skin);
        Equal(123, character.Texture1);
        Equal(456, character.Texture2);
        Equal(-1, character.Equipment[2]);
        Equal(650, character.HitPoints);
        Equal(200, character.MagicPoints);
        var stats = new ClassStats(XElement.Parse("<ClassStats objectType='782'><BestFame>400</BestFame><BestLevel>20</BestLevel></ClassStats>"));
        Equal(782, stats.ObjectType);
        Equal(800, FameUtils.NextStarFame(stats.BestFame, character.CurrentFame));
        Equal(-1, FameUtils.NextStarFame(2000, 0));

        var item = new NewsItem(XElement.Parse("<Item><Date>100000</Date><Link>fame:42</Link></Item>"));
        foreach (var (elapsed, expected) in new (long, string)[] {
            (-1, "now"), (0, "now"), (1, "1 secs"), (59, "59 secs"), (60, "1 mins"),
            (3599, "59 mins"), (3600, "1 hours"), (86399, "23 hours"), (86400, "1 days")
        }) Equal(expected, item.RelativeTime(100000 + elapsed));
        Equal(true, item.TryGetFameCharacter(out var id));
        Equal(42, id);
        Equal(false, item.TryGetWebLink(out _));
        foreach (var link in new[] { "javascript:alert(1)", "file:///tmp/news", "fame:no", "fame:-1", "" }) {
            var invalid = new NewsItem(new XElement("Item", new XElement("Link", link)));
            Equal(false, invalid.TryGetWebLink(out _));
            Equal(false, invalid.TryGetFameCharacter(out _));
        }
        Equal(0, NewsData.FromCharacterList(new XElement("Chars")).NewsList.Length);
        Equal(1, NewsData.FromCharacterList(XElement.Parse("<Chars><NewsItem><Title>Legacy</Title></NewsItem></Chars>")).NewsList.Length);
        Equal("1st", CharacterSelectionLayout.Ordinal(1));
        Equal("2nd", CharacterSelectionLayout.Ordinal(2));
        Equal("3rd", CharacterSelectionLayout.Ordinal(3));
        Equal("11th", CharacterSelectionLayout.Ordinal(11));
        Equal("12th", CharacterSelectionLayout.Ordinal(12));
        Equal("13th", CharacterSelectionLayout.Ordinal(13));
        Equal("21st", CharacterSelectionLayout.Ordinal(21));
        Equal(126, CharacterSelectionLayout.ListHeight(0, 1));
        Equal(441, CharacterSelectionLayout.ListHeight(6, 0));
        Equal(67, CharacterSelectionLayout.RowY(1));
        Equal(60, CharacterSelectionLayout.NewsY(1));
        Equal(105, CharacterSelectionLayout.DividerY);
        Equal(26, CharacterSelectionLayout.QuestIconY);
        Equal(24, CharacterSelectionLayout.QuestTextY);
        Equal(40, CharacterSelectionLayout.NewsIconSize);
        Equal(36, CharacterSelectionLayout.NewsOryxIconSize);
        Equal(20, CharacterSelectionLayout.CurrencyIconSize);
        Equal(2, CharacterSelectionLayout.CurrencyTextGap);
        Equal(10, CharacterSelectionLayout.CurrencyPairGap);
        Equal(2, CharacterSelectionLayout.CurrencyRightInset);
        Equal(24, CharacterSelectionLayout.CurrencyTopY);
        Equal((798, 24), CharacterSelectionLayout.CurrencyPosition(800, 1f, 1f));
        Equal((1596, 48), CharacterSelectionLayout.CurrencyPosition(1600, 2f, 2f));
        Equal((-72, 5, -102, -134), CurrencyDisplay.ComputePositions(50, 10, 30));
        Equal(524, TitleMenuRibbon.TopY);
        Equal(52, TitleMenuRibbon.RibbonHeight);
        var viewport = new FlashViewport(1280, 720);
        var window = viewport.DesignToWindow(new FlashDesignPoint(10, 116));
        if (MathF.Round(window.X) != 172 || MathF.Round(window.Y) != 139) throw new Exception("Selection content is not centered at 1280x720.");
        var design = viewport.WindowToDesign(window);
        if (Math.Abs(design.X - 10) > 0.001f || Math.Abs(design.Y - 116) > 0.001f) throw new Exception("Selection hit coordinates do not round-trip.");
        DeleteXGraphicAsset();
    }

    // The delete button on CurrentCharacterRect renders the Flash DeleteXGraphic
    // baked to Content/Ui/CharacterList/DeleteXGraphic.png and packed via Ui.atlas.
    private static void DeleteXGraphicAsset() {
        var root = FindRepoRoot();
        var atlasPath = Path.Combine(root, "AlloyClient", "Content", "Ui.atlas");
        var doc = XElement.Load(atlasPath);
        var entry = doc.Elements("Image")
            .FirstOrDefault(e => e.GetAttribute("name", "") == "CharacterList/DeleteXGraphic")
            ?? throw new Exception("Ui.atlas is missing the CharacterList/DeleteXGraphic image.");
        var pngPath = Path.Combine(root, "AlloyClient", "Content", "Ui", entry.Value.Trim());
        if (!File.Exists(pngPath)) throw new Exception($"DeleteXGraphic png missing at {pngPath}.");
        using var png = File.OpenRead(pngPath);
        Span<byte> header = stackalloc byte[26];
        if (png.Read(header) != header.Length) throw new Exception("DeleteXGraphic png is truncated.");
        if (header[24] != 8 || header[25] != 6) throw new Exception("DeleteXGraphic png must be 8-bit RGBA.");
        var width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        var height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        Equal(20, width);
        Equal(20, height);
    }

    private static string FindRepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent) {
            if (File.Exists(Path.Combine(dir.FullName, "AlloyClient", "Content", "Ui.atlas"))) return dir.FullName;
        }
        throw new Exception("Could not locate the repository root from the test output directory.");
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}.");
    }
}
