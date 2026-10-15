namespace Kanban.Domain.Boards;

/// <summary>
/// Posiciones decimales con huecos: al insertar entre dos elementos se usa el punto medio,
/// así mover una tarjeta solo modifica esa tarjeta. Si el hueco se agota, se renumera la lista.
/// </summary>
public static class Positioning
{
    public const double Step = 1024;
    public const double MinGap = 1e-6;

    public static double Between(double? before, double? after) => (before, after) switch
    {
        (null, null) => Step,
        ({ } b, null) => b + Step,
        (null, { } a) => a - Step,
        ({ } b, { } a) => b + (a - b) / 2,
    };

    public static bool HasRoom(double? before, double? after) =>
        before is null || after is null || after.Value - before.Value > MinGap;

    /// <summary>
    /// Posición para insertar en <paramref name="index"/> dentro de <paramref name="ordered"/>.
    /// Si no queda hueco, invoca <paramref name="rebalance"/>, que debe dejar las posiciones como (i + 1) * Step.
    /// </summary>
    public static double At(IReadOnlyList<double> ordered, int index, Action rebalance)
    {
        index = Math.Clamp(index, 0, ordered.Count);
        double? before = index > 0 ? ordered[index - 1] : null;
        double? after = index < ordered.Count ? ordered[index] : null;

        if (!HasRoom(before, after))
        {
            rebalance();
            before = index > 0 ? index * Step : null;
            after = index < ordered.Count ? (index + 1) * Step : null;
        }
        return Between(before, after);
    }
}
