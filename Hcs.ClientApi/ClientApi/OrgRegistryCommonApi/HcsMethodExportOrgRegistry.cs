
using Hcs.ClientApi.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using OrgRegistryCommon = Hcs.Service.Async.OrgRegistryCommon.v14_5_0_1;

namespace Hcs.ClientApi.OrgRegistryCommonApi
{
    /// <summary>
    /// Операции экспорта данных из реестра организаций ГИС ЖКХ.
    /// </summary>
    public class HcsMethodExportOrgRegistry : HcsOrgRegistryCommonMethod
    {
        public HcsMethodExportOrgRegistry(HcsClientConfig config) : base(config)
        {
            // запрос возвращает мало данных и исполняется быстро, можно меньше ждать
            EnableMinimalResponseWaitDelay = true;
            CanBeRestarted = true;
        }

        /// <summary>
        /// Возвращает карточки организации в ГИС ЖКХ по номеру ОГРН организации.
        /// При отсутствии результатов будет выброшено HcsNoResultsRemoteException.
        /// </summary>
        public async Task<IEnumerable<ГисОрганизация>> GetOrgByOgrn(
            string ogrn, string kpp, CancellationToken token)
        {
            if (string.IsNullOrEmpty(ogrn)) throw new ArgumentException("Не указан ОГРН для поиска организации");
            if (ogrn.Length != ГисОрганизация.ДлинаОГРН && ogrn.Length != ГисОрганизация.ДлинаОГРНИП) {
                throw new ArgumentException(
                    $"В строке ОГРН допускается или {ГисОрганизация.ДлинаОГРН} или {ГисОрганизация.ДлинаОГРНИП} символов: {ogrn}");
            }

            // критерий поиска по ОГРН и КПП
            var criteria = new OrgRegistryCommon.exportOrgRegistryRequestSearchCriteria();
            if (!string.IsNullOrEmpty(kpp)) {
                criteria.ItemsElementName = [OrgRegistryCommon.ItemsChoiceType3.OGRN, OrgRegistryCommon.ItemsChoiceType3.KPP];
                criteria.Items = [ogrn, kpp];
            }
            else { // если КПП не указан, поиск только по ОГРН
                if (ogrn.Length == ГисОрганизация.ДлинаОГРНИП) 
                    criteria.ItemsElementName = [OrgRegistryCommon.ItemsChoiceType3.OGRNIP];
                else criteria.ItemsElementName = [OrgRegistryCommon.ItemsChoiceType3.OGRN];
                criteria.Items = [ogrn];
            }

            var request = new OrgRegistryCommon.exportOrgRegistryRequest {
                Id = HcsConstants.SignedXmlElementId,
                SearchCriteria = [criteria]
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var response = await portClient.exportOrgRegistryAsync(CreateRequestHeader(), request);
                return response.AckRequest.Ack;
            }, token);

            // В возвращаемой структуре мало ценной информации, только ГУИД организации в ГИС ЖКХ
            // (необходимый для размещения договоров) и ГУИД поставщика информации OrgPPAGUID.
            // Для организаций и филиалами может вернуться список из нескольких ГУИД.
            return stateResult.Items
                .OfType<OrgRegistryCommon.exportOrgRegistryResultType>()
                .Select(x => Adopt(x));
        }

        private ГисОрганизация Adopt(OrgRegistryCommon.exportOrgRegistryResultType orgResult)
        {
            if (orgResult.OrgVersion == null) 
                throw new HcsException("В структуре exportOrgRegistryResultType не указано поле OrgVersion");

            var организация = new ГисОрганизация() {
                ГуидОрганизации = ParseGuid(orgResult.orgRootEntityGUID),
                ГуидВерсииОрганизации = ParseGuid(orgResult.OrgVersion.orgVersionGUID),
                Действующая = orgResult.OrgVersion.IsActual
            };

            switch (orgResult.OrgVersion.Item) {

                case OrgRegistryCommon.LegalType legal:
                    организация.ТипОрганизации = ГисТипОрганизации.ЮЛ;
                    организация.ИНН = legal.INN;
                    организация.КПП = legal.KPP;
                    организация.ОГРН = legal.OGRN;
                    организация.ОКОПФ = legal.OKOPF;
                    организация.КраткоеИмяОрганизации = legal.ShortName;
                    организация.ПолноеИмяОрганизации = legal.FullName;
                    организация.ЮридическийАдрес = legal.Address;
                    if (legal.ActivityEndDateSpecified)
                        организация.ДатаЛиквидации = legal.ActivityEndDate;
                    break;

                case OrgRegistryCommon.EntpsType entps:
                    организация.ТипОрганизации = ГисТипОрганизации.ИП;
                    организация.ИНН = entps.INN;
                    организация.ОГРН = entps.OGRNIP;
                    организация.Фамилия = entps.Surname;
                    организация.Имя = entps.FirstName;
                    организация.Отчество = entps.Patronymic;
                    break;

                case OrgRegistryCommon.SubsidiaryType sub:
                    организация.ТипОрганизации = ГисТипОрганизации.Филиал;
                    организация.ИНН = sub.INN;
                    организация.КПП = sub.KPP;
                    организация.ОГРН = sub.OGRN;
                    организация.ОКОПФ = sub.OKOPF;
                    организация.КраткоеИмяОрганизации = sub.ShortName;
                    организация.ПолноеИмяОрганизации = sub.FullName;
                    организация.ЮридическийАдрес = sub.Address;
                    if (sub.ActivityEndDateSpecified)
                        организация.ДатаЛиквидации = sub.ActivityEndDate;
                    break;

                case OrgRegistryCommon.ForeignBranchType foreign:
                    организация.ТипОрганизации = ГисТипОрганизации.Иностранный;
                    организация.ИНН = foreign.INN;
                    организация.КПП = foreign.KPP;
                    организация.КраткоеИмяОрганизации = foreign.ShortName;
                    организация.ПолноеИмяОрганизации = foreign.FullName;
                    организация.ЮридическийАдрес = foreign.Address;
                    break;
            }

            return организация;
        }
    }
}
