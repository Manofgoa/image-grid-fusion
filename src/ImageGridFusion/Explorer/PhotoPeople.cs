namespace ImageGridFusion.Explorer;

/// <summary>
/// A person of a photo, for its tile's People submenu: the group it opens — the top group, for a face of
/// a sub-group — or none for a face alone; the face of the photo that put it in the list; its name as the
/// tile's tooltip names it ("" for none); its files; whether it is hidden.
/// </summary>
internal sealed record PhotoPerson(FaceGroup? Group, FaceRef Face, string Name, int FileCount, bool Hidden);

/// <summary>
/// The people a photo's faces belong to (workfiles/20261028-open-people-from-image.md § People Submenu):
/// one per group — a sub-group's face counting for the group it was merged into — and one per face alone;
/// the visible ones first, then the hidden ones, each part named A→Z, then the unnamed, the larger first.
/// </summary>
internal static class PhotoPeople
{
    public static IReadOnlyList<PhotoPerson> Of(PeopleSnapshot snapshot, string fullPath, IEnumerable<FaceBox> faces)
    {
        var people = new List<PhotoPerson>();
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var box in faces)
        {
            string key = FaceKeys.Of(fullPath, box.Bounds);
            if (!snapshot.FaceByKey.TryGetValue(key, out var face))
            {
                // Not grouped yet: the faces were found after the last grouping.
                continue;
            }

            if (snapshot.OwnerOf.TryGetValue(key, out var group))
            {
                var top = group.Parent ?? group;
                if (groups.Add(top.Id))
                {
                    string name = group.Name.Length > 0 ? group.Name : top.Name;
                    people.Add(new PhotoPerson(top, face, name, top.FileCount, group.Hidden || top.Hidden));
                }
            }
            else
            {
                people.Add(new PhotoPerson(null, face, "", 1, false));
            }
        }

        people.Sort(Compare);
        return people;
    }

    private static int Compare(PhotoPerson a, PhotoPerson b)
    {
        if (a.Hidden != b.Hidden)
        {
            return a.Hidden ? 1 : -1;
        }

        bool aNamed = a.Name.Length > 0;
        bool bNamed = b.Name.Length > 0;
        if (aNamed != bNamed)
        {
            return aNamed ? -1 : 1;
        }

        int order = aNamed ? StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name) : b.FileCount.CompareTo(a.FileCount);
        return order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(a.Face.Key, b.Face.Key);
    }
}
