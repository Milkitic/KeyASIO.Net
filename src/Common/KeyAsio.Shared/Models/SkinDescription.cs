using System.Text;

namespace KeyAsio.Shared.Models;

public record SkinDescription(string FolderName, string Folder, string? Name, string? Author)
{
    public string Description
    {
        get
        {
            if (FolderName == "{classic}") return "classic";
            if (FolderName == "{internal}") return "ProMix™ Snare";

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
            if (FolderName == "{classic}")
            {
                return "Original copyright © ppy Pty Ltd.";
            }

            if (FolderName == "{internal}")
            {
                return "Copyright © KeyAsio Team";
            }

            if (Author == null)
            {
                return "Unknown author";
            }

            return $"Skin made by {Author}";
        }
    }

    public static SkinDescription Internal { get; } = new("{internal}", "{internal}", null, null);
    public static SkinDescription Classic { get; } = new("{classic}", "{classic}", null, null);
}