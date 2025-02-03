
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;
using Hcs.ClientApi.RemoteCaller;
using Hcs.Service.Async.HouseManagement.v14_5_0_1;
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Метод отправки в ГИС проекта договора ресурсоснабжения, удаления
    /// проекта договора РСО, перевода проекта в статус Размещенные.
    /// </summary>
    public class HcsMethodImportSupplyResourceContractProject : HcsHouseManagementMethod
    {
        public HcsMethodImportSupplyResourceContractProject
            (HcsClientConfig config) : base(config)
        {
            // запрос возвращает мало данных и исполняется быстро, можно меньше ждать
            EnableMinimalResponseWaitDelay = true;

            // этот метод нельзя исполнять многократно
            CanBeRestarted = false;
        }

        /// <summary>
        /// Выполнение удаления в ГИС проекта договора.
        /// </summary>
        public async Task DeleteContractProject(ГисДоговор договор, CancellationToken token)
        {
            await DoContractProjectOperation(
                договор, Item1ChoiceType10.DeleteContractProject, token);
        }

        /// <summary>
        /// Выполнение перевода проекта договора в статус Размещен.
        /// </summary>
        public async Task PlaceContractProject(ГисДоговор договор, CancellationToken token)
        {
            await DoContractProjectOperation(
                договор, Item1ChoiceType10.PlacingContractProject, token);
        }

        private async Task DoContractProjectOperation(
            ГисДоговор договор, Item1ChoiceType10 operationType, CancellationToken token)
        {
            if (договор == null) throw new ArgumentNullException(nameof(договор));
            if (договор.ГуидВерсииДоговора == default)
                throw new ArgumentException("Для проекта договора не указан ГУИД версии");

            var contract = new HouseManagement.importSupplyResourceContractProjectRequestContract() {
                TransportGUID = FormatGuid(Guid.NewGuid()),
                ItemElementName = ItemChoiceType29.ContractRootGUID,
                Item = FormatGuid(договор.ГуидДоговора),
                // если удалять версию проекта то остается предыдущая версия проекта
                //ItemElementName = ItemChoiceType29.ContractGUID,
                //Item = FormatGuid(договор.ГуидВерсииДоговора),
                Item1ElementName = operationType,
                Item1 = true,
            };

            var request = new HouseManagement.importSupplyResourceContractProjectRequest() {
                Id = HcsConstants.SignedXmlElementId,
                Contract = [ contract ],
                //version = "13.1.1.1" // версия указана в API 
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var ackResponse = await portClient.importSupplyResourceContractProjectDataAsync(
                    CreateRequestHeader(), request);
                return ackResponse.AckRequest.Ack;
            }, token);

            ParseSingleImportResult(stateResult);
        }
    }
}
