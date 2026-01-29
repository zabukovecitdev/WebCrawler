namespace Samobot.Domain.Models;

public class ParsedDocument
{
    // Basic metadata
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Canonical { get; set; } = string.Empty;
    
    // Content
    public List<ParsedHeading> Headings { get; set; } = new();
    public string BodyText { get; set; } = string.Empty;
    
    // Links and media
    public List<ParsedLink> Links { get; set; } = new();
    public List<ParsedImage> Images { get; set; } = new();
    
    // SEO
    public RobotsDirectives RobotsDirectives { get; set; } = new();
    
    // Structured data
    public Dictionary<string, string> OpenGraphData { get; set; } = new();
    public Dictionary<string, string> TwitterCardData { get; set; } = new();
    public List<string> JsonLdData { get; set; } = new();
}

public class ParsedLink
{
    public string Url { get; set; } = string.Empty;
    public string LinkText { get; set; } = string.Empty;
    public string Rel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsNoFollow { get; set; }
}

public class ParsedImage
{
    public string Src { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Width { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
}

public class ParsedHeading
{
    public int Level { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class RobotsDirectives
{
    public string Content { get; set; } = string.Empty;
    public bool NoIndex { get; set; }
    public bool NoFollow { get; set; }
    public bool NoArchive { get; set; }
    public bool NoSnippet { get; set; }
    public bool None { get; set; }
}