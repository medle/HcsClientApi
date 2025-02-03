
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Методы ГИС ЖКХ сервиса hcs-house-management (Договоры, ЛицевыеСчета, Приборы учета) 
    /// </summary>
    public class HcsHouseManagementApi
    {
        public HcsClientConfig Config { get; private set; }

        public HcsHouseManagementApi(HcsClientConfig config)
        {
            this.Config = config;
        }

        /// <summary>
        /// Размещает договор и возвращает дату размещения.
        /// </summary>
        public async Task<DateTime> РазместитьДоговор(
            ГисДоговор договор, IEnumerable<ГисАдресныйОбъект> адреса, CancellationToken token = default)
        {
            var method = new HcsMethodImportSupplyResourceContractData(Config);
            return await method.ImportContract(договор, адреса, token);
        }

        /// <summary>
        /// Размещает лицевой счет и возвращает его ЕЛС.
        /// </summary>
        public async Task<string> РазместитьЛицевойСчет(
            ГисДоговор договор, ГисЛицевойСчет лицевойСчет, CancellationToken token = default)
        {
            var method = new HcsMethodImportAccountData(Config);
            return await method.ImportAccount(договор, лицевойСчет, token);
        }

        /// <summary>
        /// Размещает прибор учета и возвращает его ГУИД.
        /// </summary>
        public async Task<Guid> РазместитьПриборУчета(
            ГисПриборУчета прибор, CancellationToken token = default)
        {
            var method = new HcsMethodImportMeteringDeviceData(Config);
            return await method.ImportMeteringDevice(прибор, token);
        }

        public async Task<DateTime> АрхивироватьПриборУчета(
            ГисПриборУчета прибор, CancellationToken token = default)
        {
            var method = new HcsMethodImportMeteringDeviceData(Config);
            return await method.ArchiveMeteringDevice(прибор, token);
        }

        public async Task<DateTime> РасторгнутьДоговор(
            ГисДоговор договор, DateTime датаРасторжения, CancellationToken token = default)
        {
            var method = new HcsMethodImportSupplyResourceContractData(Config);
            return await method.TerminateContract(договор, датаРасторжения, token);
        }

        public async Task<DateTime> АннулироватьДоговор(
            ГисДоговор договор, string причина, CancellationToken token = default)
        {
            var method = new HcsMethodImportSupplyResourceContractData(Config);
            return await method.AnnulContract(договор, причина, token);
        }

        public async Task УдалитьПроектДоговора(
            ГисДоговор договор, CancellationToken token = default)
        {
            var method = new HcsMethodImportSupplyResourceContractProject(Config);
            await method.DeleteContractProject(договор, token);
        }

        /// <summary>
        /// Переводит проект договора в состояние "Размещен".
        /// </summary>
        public async Task РазместитьПроектДоговора(
            ГисДоговор договор, CancellationToken token = default)
        {
            var method = new HcsMethodImportSupplyResourceContractProject(Config);
            await method.PlaceContractProject(договор, token);
        }

        /// <summary>
        /// Получение одного договора ресурсоснабжения по его ГУИД.
        /// Если такого договора нет, будет выброшено HcsNoResultsRemoteException.
        /// </summary>
        public async Task<ГисДоговор> ПолучитьДоговорРСО(
            Guid гуидДоговора, CancellationToken token = default)
        {
            var method = new HcsMethodExportSupplyResourceContractData(Config);
            method.EnableMinimalResponseWaitDelay = true;
            return await method.QueryOne(гуидДоговора, token);
        }

        /// <summary>
        /// Получение одного договора ресурсоснабжения по его номеру.
        /// Если такого договора нет, будет выброшено HcsNoResultsRemoteException.
        /// </summary>
        public async Task<ГисДоговор[]> ПолучитьДоговорыРСО(
            string номерДоговора, CancellationToken token = default)
        {
            var method = new HcsMethodExportSupplyResourceContractData(Config);
            method.EnableMinimalResponseWaitDelay = true;
            return await method.QueryByContractNumber(номерДоговора, token);
        }

        /// <summary>
        /// Получение списка договоров ресурсоснабжения.
        /// </summary>
        public async Task<int> ПолучитьДоговорыРСО(
            Action<ГисДоговор> resultHandler, CancellationToken token = default)
        {
            var method = new HcsMethodExportSupplyResourceContractData(Config);
            return await method.QueryAll(resultHandler, token);
        }

        /// <summary>
        /// Запрос на экспорт объектов жилищного фонда из договора ресурсоснабжения.
        /// </summary>
        public async Task<int> ПолучитьАдресаДоговораРСО(
            ГисДоговор договор, Action<ГисАдресныйОбъект> resultHandler, CancellationToken token = default)
        {
            var method = new HcsMethodExportSupplyResourceContractObjectAddress(Config);
            return await method.QueryAddresses(договор, resultHandler, token);
        }

        /// <summary>
        /// Размещение измененияч списка адресных объектов в договоре.
        /// </summary>
        public async Task РазместитьАдресаДоговораРСО(
            ГисДоговор договор,
            IEnumerable<ГисАдресныйОбъект> адресаДляРазмещения,
            IEnumerable<ГисАдресныйОбъект> адресаДляУдаления,
            CancellationToken token)
        {
            var method = new HcsMethodImportSupplyResourceContractObjectAddress(Config);
            await method.ImportObjectAddresses(договор, адресаДляРазмещения, адресаДляУдаления, token);
        }

        /// <summary>
        /// Получение списка лицевых счетов для одного здания.
        /// </summary>
        public async Task<int> ПолучитьЛицевыеСчетаПоЗданию(
            Guid fiasHouseGuid, Action<ГисЛицевойСчет> resultHandler, CancellationToken token = default)
        {
            var method = new HcsMethodExportAccountData(Config);
            return await method.Query(fiasHouseGuid, null, resultHandler, token);
        }

        /// <summary>
        /// Получение списка приборов учета для одного здания.
        /// </summary>
        public async Task<int> ПолучитьПриборыУчетаПоЗданию(
            Guid fiasHouseGuid, Action<ГисПриборУчета> resultHandler, CancellationToken token = default)
        {
            var method = new HcsMethodExportMeteringDeviceData(Config);
            return await method.ExportByHouse(fiasHouseGuid, resultHandler, token);
        }

        /// <summary>
        /// Пример получения данных об одном здании по его идентификатору в ФИАС.
        /// </summary>
        public async Task<ГисЗдание> ПолучитьЗданиеПоГуидФиас(Guid fiasHouseGuid, CancellationToken token = default)
        {
            try {
                var method = new HcsMethodExportHouse(Config);
                return await method.ExportHouseByFiasGuid(fiasHouseGuid, token);
            }
            catch (HcsException e) {
                throw new HcsException($"Не удалось получить здание по ФИАС GUID {fiasHouseGuid}", e);
            }
        }

        public async Task<ГисДоговорыИПриборы> ПолучитьВсеДоговорыИПриборы(
            Func<ГисДоговор, bool> contractFilter, CancellationToken token = default)
        {
            return await (new HcsContractRegistryDownloader(this))
                .ПолучитьВсеДоговорыИПриборы(contractFilter, token);
        }
    }
}
