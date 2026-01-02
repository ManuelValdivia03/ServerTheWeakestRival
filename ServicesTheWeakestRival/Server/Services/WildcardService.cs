using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Wildcards;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(
    InstanceContextMode = InstanceContextMode.Single,
    ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class WildcardService : IWildcardService
    {
        private readonly WildcardLogic _logic;

        public WildcardService()
        {
            _logic = new WildcardLogic();
        }

        public ListWildcardTypesResponse ListWildcardTypes(ListWildcardTypesRequest request)
        {
            return _logic.ListWildcardTypes(request);
        }

        public GetPlayerWildcardsResponse GetPlayerWildcards(GetPlayerWildcardsRequest request)
        {
            return _logic.GetPlayerWildcards(request);
        }

        public UseWildcardResponse UseWildcard(UseWildcardRequest request)
        {
            return _logic.UseWildcard(request);
        }
    }
}
