using System.Text;

namespace KeyAsio.Shared.Models;

public record SkinDescription(string FolderName, string Folder, string? Name, string? Author)
{
    public string Description
    {
        get
        {
            if (Folder == "") return "classic";
            var sb = new StringBuilder(FolderName);
            if (Name == null && Author == null) return sb.ToString();
            sb.Append(" (");
            if (Name != null) sb.Append(Name);
            if (Author != null)
            {
                if (Name != null) sb.Append(' ');
                sb.Append($"by {Author}");
            }

            sb.Append(')');
            return sb.ToString();
        }
    }

    public string? CopyRight
    {
        get
        {
            if (Folder == "")
            {
                return "Original copyright © ppy Pty Ltd.";
            }

            if (Author == null)
            {
                return "Unknown author";
            }

            return $"Skin made by {Author}";
        }
    }

    public static SkinDescription Default { get; } = new("", "", null, null);
}