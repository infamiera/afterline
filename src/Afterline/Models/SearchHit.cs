namespace Afterline.Models;

public sealed record SearchHit(string FilePath, int LineNumber, string Line, string Context)
{
    public string FileName => Path.GetFileName(FilePath);
    public string Display => $"{FileName}  ·  line {LineNumber}\n{Context}";
}
