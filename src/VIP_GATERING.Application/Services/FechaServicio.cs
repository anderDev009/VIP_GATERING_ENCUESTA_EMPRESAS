namespace VIP_GATERING.Application.Services;

public interface IFechaServicio
{
    DateOnly Hoy();
    (DateOnly inicio, DateOnly fin) RangoSemanaSiguiente();
}

public class FechaServicio : IFechaServicio
{
    public DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    public (DateOnly inicio, DateOnly fin) RangoSemanaSiguiente()
    {
        var hoy = Hoy();
        // Lunes como inicio de semana
        int delta = DayOfWeek.Monday - hoy.DayOfWeek;
        if (delta <= 0) delta += 7; // siguiente lunes
        var inicio = hoy.AddDays(delta);
        var fin = inicio.AddDays(4); // Lunes a Viernes
        return (inicio, fin);
    }
}

