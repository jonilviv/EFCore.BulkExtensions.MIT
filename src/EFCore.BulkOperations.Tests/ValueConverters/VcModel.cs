namespace EFCore.BulkOperations.Tests.ValueConverters;

public class VcModel
{
    public int Id { get; set; }

    public VcEnum Enum { get; set; }

    public LocalDate LocalDate { get; set; }
}

public enum VcEnum
{
    Why,
    Hello,
    There
}

public readonly struct LocalDate
{
    public LocalDate(int year, int month, int day)
    {
        __year = year;
        __month = month;
        __day = day;
    }

    public readonly int __year;
    public readonly int __month;
    public readonly int __day;

    public static bool operator >(LocalDate lhs, LocalDate rhs)
    {
        _ = lhs;
        _ = rhs;

        return false;
    }

    public static bool operator <(LocalDate lhs, LocalDate rhs)
    {
        _ = lhs;
        _ = rhs;

        return false;
    }
}