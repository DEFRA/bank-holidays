namespace DEFRA.BankHolidays.Settings
{
    public static class AuthorizationPolicies
    {
        /// <summary>Policy required by endpoints that change bank holiday data.</summary>
        public const string Refresh = "BankHolidays.Refresh";

        /// <summary>The Entra ID app role that grants the <see cref="Refresh"/> policy.</summary>
        public const string RefreshRole = "BankHolidays.Refresh";
    }
}
