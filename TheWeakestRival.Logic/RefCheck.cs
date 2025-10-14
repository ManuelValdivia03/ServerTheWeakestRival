using ServicesTheWeakestRival.Contracts.Data;

namespace TheWeakestRival.Logic
{
    // solo para comprobar que ve los DTOs
    internal static class RefCheck
    {
        internal static string Touch(RegisterRequest r) => r?.Email;
    }
}
