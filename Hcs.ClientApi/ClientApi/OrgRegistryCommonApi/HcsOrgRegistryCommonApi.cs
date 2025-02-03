
using Hcs.ClientApi.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hcs.ClientApi.OrgRegistryCommonApi
{
    public class HcsOrgRegistryCommonApi
    {
        public HcsClientConfig Config { get; private set; }

        public HcsOrgRegistryCommonApi(HcsClientConfig config)
        {
            this.Config = config;
        }

        /// <summary>
        /// Возвращает ГУИДы действующих организаций в ГИС ЖКХ по номеру ОГРН (КПП может быть не указан).
        /// Если организации не найдены, возвращается пустой список.
        /// </summary>
        public async Task<IEnumerable<Guid>> GetOrgRootEntityGuidByOgrn(
            string ogrn, string kpp, CancellationToken token = default)
        {
            var orgs = await GetOrgByOgrn(ogrn, kpp, token);
            return orgs.Where(x => x.Действующая).Select(x => x.ГуидОрганизации);
        }

        /// <summary>
        /// Возвращает карточки организации в ГИС ЖКХ по номеру ОГРН (КПП может быть не указан).
        /// Если организации не найдены, возвращается пустой список.
        /// </summary>
        public async Task<IEnumerable<ГисОрганизация>> GetOrgByOgrn(
            string ogrn, string kpp, CancellationToken token = default)
        {
            try {
                var method = new HcsMethodExportOrgRegistry(Config);
                return await method.GetOrgByOgrn(ogrn, kpp, token);
            }
            catch (HcsNoResultsRemoteException) {
                return [];
            }
        }
    }
}
