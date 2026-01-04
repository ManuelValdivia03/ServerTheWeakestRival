namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal interface IWildcardRepository
    {
        void GrantLightningWildcard(long matchDbId, int userId);
    }
}
