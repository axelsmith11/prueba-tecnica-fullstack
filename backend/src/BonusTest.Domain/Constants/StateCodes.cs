namespace BonusTest.Domain.Constants;

public static class MasterStateCodes
{
    public const string Employee = "01";
    public const string Bonus = "02";
}

public static class EmployeeStates
{
    public const string Active = "0101";
    public const string Terminated = "0102";
}

public static class BonusStates
{
    public const string Registered = "0201";
    public const string Paid = "0202";
    public const string Cancelled = "0203";
}

public static class BonusTypes
{
    public const int FixedAmount = 1;
    public const int SalaryPercentage = 2;
}
