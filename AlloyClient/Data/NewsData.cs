using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Alloy.Common;

namespace AlloyClient.Data;

public sealed class NewsData(IEnumerable<XElement> xmls) : IGlobalData {
    public readonly NewsItem[] NewsList = xmls.Select(n => new NewsItem(n)).ToArray();

    public static NewsData FromCharacterList(XElement xml) {
        return new NewsData(xml.Elements("News").Elements("Item").Concat(xml.Elements("NewsItem")));
    }
}

public sealed class NewsItem(XElement xml) {
    public readonly string Icon = xml.GetValue("Icon", "");

    public readonly string Title = xml.GetValue("Title", "");

    public readonly string TagLine = xml.GetValue("TagLine", "");

    public readonly string Link = xml.GetValue("Link", "");

    public readonly int Date = xml.GetValue("Date", 0);

    public string RelativeTime(long now) {
        var elapsed = now - Date;
        return elapsed switch {
            <= 0 => "now",
            < 60 => $"{elapsed} secs",
            < 3600 => $"{elapsed / 60} mins",
            < 86400 => $"{elapsed / 3600} hours",
            _ => $"{elapsed / 86400} days"
        };
    }

    public bool TryGetFameCharacter(out int characterId) {
        characterId = 0;
        return Link.StartsWith("fame:", StringComparison.Ordinal)
            && int.TryParse(Link.AsSpan(5), out characterId) && characterId > 0;
    }

    public bool TryGetWebLink(out Uri uri) {
        return Uri.TryCreate(Link, UriKind.Absolute, out uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}