namespace sales_portal.core.Calculations;

public static class MoneyMath
{
    public static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal Round3(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
