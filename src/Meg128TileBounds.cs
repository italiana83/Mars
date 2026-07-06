namespace Mars;

/// <summary>Тип продукта MOLA MEG128 (префикс имени файла: megt / megr / megc).</summary>
public enum Meg128ProductKind
{
    Topography = 0,
    Radius = 1,
    Counts = 2,
}

/// <summary>Географические границы одного тайла MOLA MEG128 из PDS label (.lbl).</summary>
public readonly record struct Meg128TileBounds(
    string Name,
    float LatMin,
    float LatMax,
    float LonWest,
    float LonEast)
{
    /// <summary>Тип данных по 4-му символу имени (t/r/c).</summary>
    public Meg128ProductKind ProductKind => Name.Length >= 4
        ? char.ToLowerInvariant(Name[3]) switch
        {
            't' => Meg128ProductKind.Topography,
            'r' => Meg128ProductKind.Radius,
            'c' => Meg128ProductKind.Counts,
            _ => Meg128ProductKind.Topography,
        }
        : Meg128ProductKind.Topography;
};

