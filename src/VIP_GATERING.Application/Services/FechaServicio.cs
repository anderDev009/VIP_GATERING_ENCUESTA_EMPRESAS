namespace VIP_GATERING.Application.Services;

public interface IFechaServicio
{
    DateOnly Hoy();
    (DateOnly inicio, DateOnly fin) RangoSemanaSiguiente();
    (DateOnly inicio, DateOnly fin) RangoSemanaActual();
    DateOnly ObtenerFechaDelDia(DateOnly inicioSemana, DayOfWeek dia);
}

public class FechaServicio : IFechaServicio
{
    public DateOnly Hoy() => DateOnly.FromDateTime(DateTime.Now.Date);

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

    public (DateOnly inicio, DateOnly fin) RangoSemanaActual()
    {
        var hoy = Hoy();
        int diff = ((int)hoy.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var lunes = hoy.AddDays(-diff);
        var fin = lunes.AddDays(4);
        return (lunes, fin);
    }

    public DateOnly ObtenerFechaDelDia(DateOnly inicioSemana, DayOfWeek dia)
    {
        var offset = ((int)dia - (int)DayOfWeek.Monday + 7) % 7;
        return inicioSemana.AddDays(offset);
    }
}
