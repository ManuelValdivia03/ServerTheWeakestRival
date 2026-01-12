using ServicesTheWeakestRival.Contracts.Data;

namespace TheWeakestRival.Logic
{
    internal static class RefCheck
    {
        internal static string Touch(RegisterRequest r) => r.Email;
    }
}
